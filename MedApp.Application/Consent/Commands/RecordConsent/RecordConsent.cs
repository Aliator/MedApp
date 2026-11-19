using FluentValidation;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Consent.Common;
using MedApp.Application.Consent.Repositories;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.Repositories;
using MedApp.Contracts.Consent;
using MedApp.Domain.Consent;
using MediatR;

namespace MedApp.Application.Consent.Commands.RecordConsent;

/// <summary>Records a patient's consent to one of a pathway's stages, as the clinician states it.</summary>
public sealed record RecordConsentCommand(
    Guid PathwayId,
    Guid StageId,
    Guid ConsentFormId,
    DateOnly SignedOn,
    string SignedBy,
    Guid? ScanAttachmentId,
    PathwayActor Actor) : IRequest<ConsentRecordResponse>;

public sealed class RecordConsentValidator : AbstractValidator<RecordConsentCommand>
{
    public const string SignedByMessage =
        "Say who signed: Patient, PersonWithParentalResponsibility or BestInterestsDecision.";

    public RecordConsentValidator()
    {
        RuleFor(x => x.StageId).NotEmpty().WithMessage("Choose the stage consented to.");
        RuleFor(x => x.ConsentFormId).NotEmpty().WithMessage("Choose the consent form.");
        RuleFor(x => x.SignedOn).NotEmpty().WithMessage("Give the date it was signed.");
        RuleFor(x => x.SignedBy).Must(s => PathwayEnums.TryParse<ConsentSignedBy>(s, out _)).WithMessage(SignedByMessage);
        RuleFor(x => x.ScanAttachmentId).Must(id => id != Guid.Empty).WithMessage("Choose the scan, or leave it out.");
    }
}

/// <summary>
/// Records consent (decision 145): 404 for an unknown pathway or stage; 409 unless consent can be recorded for the stage
/// now (<see cref="ConsentWorkflow.WhyCantRecord"/>: the record can change, the pathway is open, the stage still to
/// do); 400 for an archived or unknown form, a signed date in the future or over a year back, or a scan that isn't a
/// live file on the pathway. Saved with its <c>ConsentRecorded</c> audit entry. <b>Nothing is assessed</b>: MedApp
/// records what the clinician states.
/// </summary>
public sealed class RecordConsentHandler(
    IPatientPathwayRepository pathways,
    IConsentRepository repository,
    IIdentityReadService identity,
    TimeProvider timeProvider)
    : IRequestHandler<RecordConsentCommand, ConsentRecordResponse>
{
    public const string FormMessage = "Choose a consent form that's in use (not archived).";

    public async Task<ConsentRecordResponse> Handle(RecordConsentCommand request, CancellationToken cancellationToken)
    {
        var pathway = await pathways.GetByIdAsync(request.PathwayId, cancellationToken)
                      ?? throw new NotFoundException($"Pathway '{request.PathwayId}' was not found.");
        var stage = pathway.GetStageOrThrow(request.StageId);

        if (ConsentWorkflow.WhyCantRecord(pathway, stage) is { } why)
            throw new ConflictException(why);

        var form = await repository.GetFormAsync(request.ConsentFormId, cancellationToken);
        if (form is null || form.IsArchived)
            throw new ValidationException([new FluentValidation.Results.ValidationFailure(nameof(request.ConsentFormId), FormMessage)]);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (ConsentWorkflow.WhySignedOnInvalid(request.SignedOn, UkTime.Today(timeProvider)) is { } badDate)
            throw new ValidationException([new FluentValidation.Results.ValidationFailure(nameof(request.SignedOn), badDate)]);

        if (request.ScanAttachmentId is { } scanId)
            await ConsentRecords.CheckScanAsync(repository, scanId, pathway, nameof(request.ScanAttachmentId), cancellationToken);

        var record = new ConsentRecord
        {
            Id = Guid.NewGuid(),
            PatientId = pathway.PatientId,
            TeamId = pathway.TeamId,
            PathwayId = pathway.Id,
            StageId = stage.Id,
            ConsentFormId = form.Id,
            FormName = form.Name,
            FormVersion = form.FormVersion,
            SignedOn = request.SignedOn,
            SignedBy = PathwayEnums.Parse<ConsentSignedBy>(request.SignedBy),
            RecordedByUserId = request.Actor.UserId,
            RecordedAtUtc = now,
            ScanAttachmentId = request.ScanAttachmentId,
        };

        var change = new ConsentChange(record, pathway, request.Actor, now);
        change.Recorded();
        await repository.AddAsync(record, change.AuditEntries, cancellationToken);

        // For the response: the pathway as read (its patient and stages), never saved through the record.
        record.Patient = pathway.Patient;
        record.Pathway = pathway;
        return await ConsentRecords.ToResponseAsync(record, identity, request.Actor, now, cancellationToken);
    }
}
