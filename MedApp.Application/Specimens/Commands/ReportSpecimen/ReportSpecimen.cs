using FluentValidation;
using MedApp.Application.Attachments.Commands.UploadAttachment;
using MedApp.Application.Attachments.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Notifications.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Results.Common;
using MedApp.Domain.Results;
using MedApp.Application.Specimens.Common;
using MedApp.Application.Specimens.Repositories;
using MedApp.Contracts.Specimens.Responses;
using MedApp.Domain.Attachments;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Specimens;
using MedApp.Application.Common.Teams;
using MedApp.Application.Teams.Repositories;
using MediatR;

namespace MedApp.Application.Specimens.Commands.ReportSpecimen;

/// <summary>
/// Attaches the lab's report to a received specimen: the request's body is the file, read once as it arrives, under
/// every rule an attached file has; <paramref name="ContentLength"/> is what the request said its length is.
/// </summary>
public sealed record ReportSpecimenCommand(
    Guid SpecimenId,
    string? FileName,
    long? ContentLength,
    Stream Content,
    int ExpectedVersion,
    PathwayActor Actor)
    : IRequest<SpecimenResponse>;

public sealed class ReportSpecimenValidator : AbstractValidator<ReportSpecimenCommand>
{
    public ReportSpecimenValidator()
    {
        RuleFor(x => x.SpecimenId).NotEmpty().WithMessage("Specimen id is required.");
        RuleFor(x => x.FileName)
            .Must(name => AttachmentFileName.Clean(name) is not null)
            .WithMessage("Give the file's name.")
            .MaximumLength(UploadAttachmentValidator.MaxRawFileNameLength)
            .WithMessage($"The file's name must be at most {UploadAttachmentValidator.MaxRawFileNameLength} characters.");
        RuleFor(x => x.ExpectedVersion).ExpectedVersion();
    }
}

/// <summary>
/// Reports a specimen (decision 86): 409 unless it's received and not yet reported, if the record is anonymised, or if
/// it's changed since the caller's version (allowed while restricted: a report must never be stuck, decision 75). The
/// file is taken in as any attachment is (<see cref="AttachmentIntake"/>: its type from its contents, scanned, at most
/// 20 MB) and attached to the specimen's pathway, or to the patient if the pathway no longer takes files (cancelled or
/// archived meanwhile). The file's record, its <c>AttachmentAdded</c> entry, the specimen's change and its
/// <c>SpecimenReported</c> entry, and the result it makes (decision 91: awaiting sign-off by the requesting clinician)
/// with its <c>ResultRecorded</c> entry are saved together; then the clinician is told (<c>ResultReady</c>). MedApp
/// stores and shows the report as it came: it never reads it.
/// </summary>
public sealed class ReportSpecimenHandler(
    ISpecimenRepository repository,
    IFileStore fileStore,
    IUploadStaging staging,
    IMalwareScanner scanner,
    IThumbnailGenerator thumbnails,
    IIdentityReadService identity,
    IQrCodeRenderer qrCodes,
    INotificationSender notifications,
    ITeamBoundary boundary,
    ITeamRepository teams,
    TimeProvider timeProvider)
    : IRequestHandler<ReportSpecimenCommand, SpecimenResponse>
{
    public async Task<SpecimenResponse> Handle(ReportSpecimenCommand request, CancellationToken cancellationToken)
    {
        if (!fileStore.IsConfigured)
            throw new FileStorageUnavailableException();
        if (request.ContentLength > AttachmentLimits.MaxFileBytes)
            throw new FileTooLargeException();

        var specimen = await repository.GetVisibleForUpdateOrThrowAsync(
            request.SpecimenId, boundary, teams, request.Actor.Teams, cancellationToken);
        if (SpecimenWorkflow.WhyCantMoveTo(specimen, SpecimenStatus.Reported) is { } why)
            throw new ConflictException(why);
        // A sample in a lab case (MC15) is reported by authorising the case's report, never on its own.
        if (specimen.LabCase is { } labCase)
            throw new ConflictException(InLabCaseMessage(labCase.Number));
        SpecimenWorkflow.EnsureVersion(specimen, request.ExpectedVersion);

        var fileName = AttachmentFileName.Clean(request.FileName)
                       ?? throw new ArgumentException("The validator checks the file has a name.", nameof(request));

        var pathway = specimen.Pathway;
        // The pathway if it still takes files; otherwise the patient (decision 86), so the report is never stuck.
        var onPathway = new AttachmentTarget(specimen.PatientId, pathway, null).WhyFilesCantChange() is null;
        var target = onPathway
            ? new AttachmentTarget(specimen.PatientId, pathway, null)
            : new AttachmentTarget(specimen.PatientId, null, null) { Patient = specimen.Patient };

        Result? made = null;
        var intake = new AttachmentIntake(fileStore, staging, scanner, thumbnails);
        await intake.TakeInAsync(
            fileName,
            request.Content,
            // The specimen's row version catches anyone else's change while the file arrived, when it's saved.
            _ => Task.CompletedTask,
            async stored =>
            {
                var now = timeProvider.GetUtcNow().UtcDateTime;
                var report = new Attachment
                {
                    Id = stored.Id,
                    PatientId = specimen.PatientId,
                    TeamId = specimen.TeamId,
                    PathwayId = target.Pathway?.Id,
                    FileName = stored.FileName,
                    ContentType = stored.Type.ContentType,
                    SizeBytes = stored.Size,
                    Sha256 = stored.Sha256,
                    ScanStatus = stored.ScanStatus,
                    HasThumbnail = stored.HasThumbnail,
                    UploadedByUserId = request.Actor.UserId,
                    UploadedByUsername = request.Actor.Username,
                    UploadedAtUtc = now,
                };

                var change = new SpecimenChange(specimen, pathway, request.Actor, now.AddTicks(1));
                change.Report(report.Id);
                var fileEntry = AttachmentAudit.Entry(
                    report, target, request.Actor, PatientPathwayAuditAction.AttachmentAdded, now);

                // The result the report makes, for the requesting clinician to sign off (decision 91).
                var result = new Result
                {
                    Id = Guid.NewGuid(),
                    PatientId = specimen.PatientId,
                    Patient = specimen.Patient,
                    TeamId = specimen.TeamId,
                    PathwayId = specimen.PathwayId,
                    Pathway = pathway,
                    SpecimenId = specimen.Id,
                    Specimen = specimen,
                    Source = ResultSource.Specimen,
                    Title = ResultTitle(specimen),
                    ReportAttachmentId = report.Id,
                    ReportedAtUtc = now,
                    ReportedByUserId = request.Actor.UserId,
                    ResponsibleUserId = specimen.RequestingUserId,
                };
                var responsible = await identity.GetUsernamesByIdAsync([specimen.RequestingUserId], cancellationToken);
                var resultChange = new ResultChange(result, pathway, request.Actor, now.AddTicks(2));
                resultChange.Record(responsible.GetValueOrDefault(specimen.RequestingUserId, PathwayMapper.DeletedUser));

                await repository.SaveReportAsync(
                    report, result, [fileEntry, .. change.AuditEntries, .. resultChange.AuditEntries], cancellationToken);
                made = result;
                return report;
            },
            cancellationToken);

        if (made is not null && ResultNotifications.Ready(made, request.Actor, timeProvider.GetUtcNow().UtcDateTime) is { } ready)
            await notifications.SendAsync(ready, cancellationToken);

        return await repository.ToResponseAsync(
            specimen, identity, qrCodes, request.Actor, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
    }

    /// <summary>A specimen's result's title: its type and number (admin-written and MedApp's own), "Skin biopsy (punch) MA26-000123".</summary>
    public static string InLabCaseMessage(string number) =>
        $"This sample is in lab case {number}: its report is authorised on the case.";

    public static string ResultTitle(Specimen specimen)
    {
        var title = $"{specimen.SpecimenType?.Name ?? "Specimen"} {specimen.Number}".Trim();
        return title.Length <= Result.MaxTitleLength ? title : title[..Result.MaxTitleLength];
    }
}
