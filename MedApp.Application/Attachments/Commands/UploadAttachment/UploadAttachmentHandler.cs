using MedApp.Application.Common.Teams;
using MedApp.Application.Attachments.Common;
using MedApp.Application.Attachments.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Patients.Common;
using MedApp.Application.Patients.Repositories;
using MedApp.Application.Pathways.Repositories;
using MedApp.Contracts.Attachments.Responses;
using MedApp.Domain.Attachments;
using MedApp.Domain.Pathways.PatientPathways;
using MediatR;

namespace MedApp.Application.Attachments.Commands.UploadAttachment;

/// <summary>
/// Attaches a file. In order: storage is set up and the file isn't declared too large; what it's attached to exists,
/// its patient's record can be changed (not restricted or anonymised: <see cref="PatientRules"/>), and it allows new
/// files; the file is taken in (<see cref="AttachmentIntake"/>: staged, identified from its contents, scanned); what
/// it's attached to is checked again, since the upload may have taken a minute (still there, the record still
/// changeable, the stage or pathway still taking files); it's stored, with a thumbnail if it's an image; then its
/// record and audit entry are saved together. If anything after storing fails, the stored bytes are deleted again.
/// </summary>
public sealed class UploadAttachmentHandler(
    IAttachmentRepository attachments,
    IPatientRepository patients,
    IPatientPathwayRepository pathways,
    IFileStore fileStore,
    IUploadStaging staging,
    IMalwareScanner scanner,
    IThumbnailGenerator thumbnails,
    ITeamBoundary boundary,
    TimeProvider timeProvider)
    : IRequestHandler<UploadAttachmentCommand, AttachmentResponse>
{
    /// <summary>How long deleting a failed upload's bytes may take before it's left for later.</summary>
    public static readonly TimeSpan CleanUpTimeout = AttachmentIntake.CleanUpTimeout;

    public async Task<AttachmentResponse> Handle(UploadAttachmentCommand request, CancellationToken cancellationToken)
    {
        if (!fileStore.IsConfigured)
            throw new FileStorageUnavailableException();
        if (request.ContentLength > AttachmentLimits.MaxFileBytes)
            throw new FileTooLargeException();

        await CheckTargetAsync(request, cancellationToken);

        var fileName = AttachmentFileName.Clean(request.FileName)
                       ?? throw new ArgumentException("The validator checks the file has a name.", nameof(request));

        AttachmentTarget? target = null;
        var intake = new AttachmentIntake(fileStore, staging, scanner, thumbnails);
        return await intake.TakeInAsync(
            fileName,
            request.Content,
            async ct => target = await CheckTargetAsync(request, ct),
            async stored =>
            {
                var attachment = AttachmentFor(stored, target!, request);
                var audit = AttachmentAudit.Entry(
                    attachment, target!, request.Actor, PatientPathwayAuditAction.AttachmentAdded, attachment.UploadedAtUtc);
                await attachments.AddAsync(attachment, audit, cancellationToken);

                return AttachmentMapper.ToResponse(attachment, target!, request.Actor.UserId, request.CanCorrectRecords, canAttachFiles: true);
            },
            cancellationToken);
    }

    private Attachment AttachmentFor(StoredFile stored, AttachmentTarget target, UploadAttachmentCommand request) => new()
    {
        Id = stored.Id,
        PatientId = target.PatientId,
        // Its pathway's or referral's team; a patient's own file, the uploader's (the team walls).
        TeamId = target.Pathway?.TeamId
                 ?? target.Referral?.TeamId
                 ?? request.Actor.Teams.DefaultTeamId
                 ?? MedApp.Domain.Teams.Team.GeneralId,
        PathwayId = target.Pathway?.Id,
        StageId = target.Stage?.Id,
        ReferralId = target.Referral?.Id,
        FileName = stored.FileName,
        ContentType = stored.Type.ContentType,
        SizeBytes = stored.Size,
        Sha256 = stored.Sha256,
        ScanStatus = stored.ScanStatus,
        HasThumbnail = stored.HasThumbnail,
        UploadedByUserId = request.Actor.UserId,
        UploadedByUsername = request.Actor.Username,
        UploadedAtUtc = timeProvider.GetUtcNow().UtcDateTime,
    };

    /// <summary>
    /// What the file is attached to, or a 404 if it's gone, or a 409 if the patient's record can't change
    /// (<see cref="PatientRules"/>) or it doesn't take files now.
    /// </summary>
    private async Task<AttachmentTarget> CheckTargetAsync(UploadAttachmentCommand request, CancellationToken ct)
    {
        var target = await AttachmentTargets.ResolveAsync(
            request.PatientId, request.PathwayId, request.StageId, request.ReferralId, patients, pathways, attachments, ct);
        // The team walls: a pathway or referral behind them is a 404, as if it weren't there. Seen is enough (the lab
        // attaches its reports to a pathway it was passed).
        if (target.Pathway is { } pathway)
            await boundary.EnsurePathwayVisibleAsync(
                pathway, request.Actor.Teams, Pathways.Common.PathwayRepositoryExtensions.NotFoundMessage(pathway.Id), ct);
        else if (target.Referral is { } referral)
            await boundary.EnsureTeamVisibleAsync(
                "Referral", referral.Id, referral.TeamId, request.Actor.Teams, $"Referral '{referral.Id}' was not found.", ct);
        PatientRules.EnsureCanChange(target.Owner);
        if (target.WhyFilesCantChange() is { } reason)
            throw new ConflictException(reason);
        return target;
    }
}
