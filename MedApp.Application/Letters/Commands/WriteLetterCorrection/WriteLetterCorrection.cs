using FluentValidation;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Letters.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Letters.Responses;
using MedApp.Domain.Letters;
using MediatR;

namespace MedApp.Application.Letters.Commands.WriteLetterCorrection;

/// <summary>Starts a correction of a withdrawn letter: a new draft with its text and recipients.</summary>
public sealed record WriteLetterCorrectionCommand(Guid LetterId, PathwayActor Actor) : IRequest<LetterResponse>;

public sealed class WriteLetterCorrectionValidator : AbstractValidator<WriteLetterCorrectionCommand>
{
    public WriteLetterCorrectionValidator() =>
        RuleFor(x => x.LetterId).NotEmpty().WithMessage("Letter id is required.");
}

/// <summary>
/// Writes a correction (decision 104): only of a letter withdrawn after approval (409), once (409, naming none: the
/// page links to the one there is), on a record that allows letters (409). The new draft is the writer's, with the old
/// letter's text, large print, kind and template, and its recipients (their details from the record now), pointing
/// back at it (<see cref="Letter.ReplacesLetterId"/>).
/// </summary>
public sealed class WriteLetterCorrectionHandler(LetterRecords records, TimeProvider timeProvider)
    : IRequestHandler<WriteLetterCorrectionCommand, LetterResponse>
{
    public async Task<LetterResponse> Handle(WriteLetterCorrectionCommand request, CancellationToken cancellationToken)
    {
        var old = await records.LoadAsync(request.LetterId, forUpdate: false, cancellationToken);
        var hasCorrection = await records.Letters.GetCorrectionIdAsync(old.Letter.Id, cancellationToken) is not null;
        if (!LetterWorkflow.CanBeCorrected(old.Letter, hasCorrection))
            throw new ConflictException(hasCorrection
                ? "A correction of this letter has been written already. Open it from the letter's page."
                : "Only a letter withdrawn after it was approved can be corrected.");
        LetterWorkflow.EnsureRecordAllowsLetters(old.Patient);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var letter = new Letter
        {
            Id = Guid.NewGuid(),
            PatientId = old.Letter.PatientId,
            PathwayId = old.Letter.PathwayId,
            ReferralId = old.Letter.ReferralId,
            TemplateId = old.Letter.TemplateId,
            TemplateName = old.Letter.TemplateName,
            Kind = old.Letter.Kind,
            Letterhead = old.Letter.Letterhead,
            Body = old.Letter.Body,
            Closing = old.Letter.Closing,
            LargePrint = old.Letter.LargePrint,
            Status = LetterStatus.Draft,
            AuthorUserId = request.Actor.UserId,
            ReplacesLetterId = old.Letter.Id,
            CreatedAt = now,
            LastUpdated = now,
        };
        var record = old with { Letter = letter };

        var inputs = old.Letter.Recipients
            .OrderBy(r => r.Position)
            .Select(r => new LetterRecipientInput(r.Kind, r.PracticeId, r.Name, r.AddressLines, r.Postcode, r.Email))
            .ToList();
        letter.Recipients = await records.RecipientsAsync(record, inputs, cancellationToken);

        var change = new LetterChange(letter, record.Pathway, request.Actor, now);
        change.Draft();
        await records.Letters.AddAsync(letter, change.AuditEntries, cancellationToken);

        return await records.ToResponseAsync(record, request.Actor, UkTime.DateOf(now), cancellationToken);
    }
}
