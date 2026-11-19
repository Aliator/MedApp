using FluentValidation;
using FluentValidation.Results;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Letters.Common;
using MedApp.Application.Letters.Repositories;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Patients.Common;
using MedApp.Contracts.Letters.Responses;
using MedApp.Domain.Letters;
using MedApp.Domain.Referrals;
using MediatR;

namespace MedApp.Application.Letters.Commands.CreateLetter;

/// <summary>Starts a letter from a template, about one of a patient's pathways or referrals (exactly one).</summary>
public sealed record CreateLetterCommand(Guid TemplateId, Guid? PathwayId, Guid? ReferralId, PathwayActor Actor)
    : IRequest<LetterResponse>;

public sealed class CreateLetterValidator : AbstractValidator<CreateLetterCommand>
{
    public CreateLetterValidator()
    {
        RuleFor(x => x.TemplateId).NotEmpty().WithMessage("Choose a template.");
        RuleFor(x => x)
            .Must(x => (x.PathwayId is not null) != (x.ReferralId is not null))
            .WithName("PathwayId")
            .WithMessage("A letter is about one pathway or one referral: give exactly one.");
    }
}

/// <summary>
/// Starts a draft (decision 100): the template must exist (404), not be archived (409), be the pathway's or referral's
/// team's or for every team (400), and from a referral be a referral response or a request for more
/// information (400). The record must allow letters (409). It copies the template's text; its recipients start as
/// the patient's registered practice and the patient, or for a referral the referrer (and the patient, for a reply).
/// Nothing is chosen for the author beyond that: the body is theirs to write.
/// </summary>
public sealed class CreateLetterHandler(
    ILetterTemplateRepository templates,
    LetterRecords records,
    TimeProvider timeProvider)
    : IRequestHandler<CreateLetterCommand, LetterResponse>
{
    public async Task<LetterResponse> Handle(CreateLetterCommand request, CancellationToken cancellationToken)
    {
        var template = await templates.GetOrThrowAsync(request.TemplateId, cancellationToken);
        if (template.IsArchived)
            throw new ConflictException($"The '{template.Name}' template is archived, so letters can't be started from it. Choose another.");

        var pathway = request.PathwayId is { } pathwayId
            ? await records.GetPathwayAsync(pathwayId, cancellationToken)
              ?? throw new NotFoundException($"Pathway '{pathwayId}' was not found.")
            : null;
        var referral = request.ReferralId is { } referralId
            ? await records.GetReferralAsync(referralId, cancellationToken)
              ?? throw new NotFoundException($"Referral '{referralId}' was not found.")
            : null;

        var teamId = pathway?.TeamId ?? referral!.TeamId;
        if (template.TeamId is { } templateTeam && templateTeam != teamId)
            throw Invalid(nameof(request.TemplateId), $"The '{template.Name}' template is for another team. Choose one of this {(pathway is null ? "referral" : "pathway")}'s team's.");
        if (referral is not null && !LetterWorkflow.ReferralKinds.Contains(template.Kind))
            throw Invalid(nameof(request.TemplateId), "From a referral, write a referral response or a request for more information. Write other letters from the pathway.");
        if (referral is { Status: ReferralStatus.EnteredInError })
            throw new ConflictException("This referral was entered in error, so letters can't be written about it.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var letter = new Letter
        {
            Id = Guid.NewGuid(),
            PatientId = pathway?.PatientId ?? referral!.PatientId,
            TeamId = teamId,
            PathwayId = pathway?.Id,
            ReferralId = referral?.Id,
            TemplateId = template.Id,
            TemplateName = template.Name,
            Kind = template.Kind,
            Letterhead = template.Letterhead,
            Body = template.BodySkeleton,
            Closing = template.Closing,
            Status = LetterStatus.Draft,
            AuthorUserId = request.Actor.UserId,
            CreatedAt = now,
            LastUpdated = now,
        };

        var record = await records.LoadForAsync(letter, cancellationToken);
        LetterWorkflow.EnsureRecordAllowsLetters(record.Patient);

        letter.Recipients = await records.RecipientsAsync(record, StartingRecipients(record), cancellationToken);

        var change = new LetterChange(letter, pathway, request.Actor, now);
        change.Draft();
        await records.Letters.AddAsync(letter, change.AuditEntries, cancellationToken);

        return await records.ToResponseAsync(record, request.Actor, UkTime.DateOf(now), cancellationToken);
    }

    /// <summary>
    /// Who a new letter starts addressed to (decision 101): from a referral, the referrer, and the patient for a reply;
    /// from a pathway, the patient's registered practice (if they have one, from the directory or held on the patient
    /// when it isn't listed) and the patient.
    /// </summary>
    public static IReadOnlyList<LetterRecipientInput> StartingRecipients(LetterRecord record)
    {
        var patient = new LetterRecipientInput(LetterRecipientKind.Patient, null, null, null, null, null);
        if (record.Letter.ReferralId is not null)
            return record.Letter.Kind == LetterKind.ReferralResponse
                ? [new LetterRecipientInput(LetterRecipientKind.Referrer, null, null, null, null, null), patient]
                : [new LetterRecipientInput(LetterRecipientKind.Referrer, null, null, null, null, null)];

        if (record.Patient.PracticeId is { } practiceId)
            return [new LetterRecipientInput(LetterRecipientKind.GpPractice, practiceId, null, null, null, null), patient];
        return UnlistedPractices.Has(record.Patient)
            ? [new LetterRecipientInput(LetterRecipientKind.GpPractice, null, null, null, null, null), patient]
            : [patient];
    }

    private static ValidationException Invalid(string property, string message) =>
        new([new ValidationFailure(property, message)]);
}
