using MedApp.Contracts.Attachments.Responses;
using MedApp.Domain.Attachments;

namespace MedApp.Application.Attachments.Common;

/// <summary>Attachments as the API returns them, with what the person asking may do with each.</summary>
public static class AttachmentMapper
{
    /// <summary>One attachment, with its pathway's title and stage's name now (null if none).</summary>
    public static AttachmentResponse ToResponse(AttachmentRow row, Guid viewerId, bool canCorrectRecords, bool canAttachFiles)
    {
        var attachment = row.Attachment;
        return new AttachmentResponse
        {
            Id = attachment.Id,
            PatientId = attachment.PatientId,
            AttachedTo = AttachmentTarget.KindOf(attachment).ToString(),
            PathwayId = attachment.PathwayId,
            PathwayTitle = attachment.PathwayId is null ? null : row.PathwayTitle,
            StageId = attachment.StageId,
            StageName = attachment.StageId is null ? null : row.StageName,
            ReferralId = attachment.ReferralId,
            FileName = attachment.FileName,
            ContentType = attachment.ContentType,
            TypeName = AttachmentFileTypes.FromContentType(attachment.ContentType)?.Name ?? attachment.ContentType,
            SizeBytes = attachment.SizeBytes,
            Sha256 = attachment.Sha256,
            ScanStatus = attachment.ScanStatus.ToString(),
            HasThumbnail = attachment.HasThumbnail && !attachment.IsDeleted,
            UploadedByUsername = attachment.UploadedByUsername,
            UploadedAtUtc = attachment.UploadedAtUtc,
            CanDelete = CanDelete(attachment, viewerId, canCorrectRecords, canAttachFiles),
            IsDeleted = attachment.IsDeleted,
            DeletedAtUtc = attachment.DeletedAtUtc,
            DeletedByUsername = attachment.DeletedByUsername,
            IsClinicalPhoto = attachment.IsClinicalPhoto,
            CanMarkClinicalPhoto = canAttachFiles && Consent.Common.ClinicalPhotography.CanBeClinicalPhoto(attachment),
        };
    }

    /// <summary>One attachment and what it's attached to.</summary>
    public static AttachmentResponse ToResponse(
        Attachment attachment, AttachmentTarget target, Guid viewerId, bool canCorrectRecords, bool canAttachFiles) =>
        ToResponse(new AttachmentRow(attachment, target.Pathway?.Title, target.Stage?.Name), viewerId, canCorrectRecords, canAttachFiles);

    /// <summary>A list, newest first, with how many there are in all and the limits on new files.</summary>
    public static AttachmentListResponse ToListResponse(
        IReadOnlyList<AttachmentRow> rows, int totalCount, Guid viewerId, bool canCorrectRecords, bool canAttachFiles) => new()
    {
        Items = rows.Select(row => ToResponse(row, viewerId, canCorrectRecords, canAttachFiles)).ToList(),
        TotalCount = totalCount,
        MaxFileBytes = AttachmentLimits.MaxFileBytes,
        AllowedExtensions = AttachmentFileTypes.Extensions.ToList(),
    };

    /// <summary>
    /// Whether someone may delete a file: holders of <c>CorrectRecords</c> (<paramref name="canCorrectRecords"/>) any,
    /// anyone else their own, and either only while they hold <c>AttachFiles</c> (<paramref name="canAttachFiles"/>),
    /// which the delete endpoint requires. Whether the patient, pathway or stage allows it now is checked when they try
    /// (<see cref="AttachmentTarget.WhyFilesCantChange"/>).
    /// </summary>
    public static bool CanDelete(Attachment attachment, Guid viewerId, bool canCorrectRecords, bool canAttachFiles) =>
        !attachment.IsDeleted && canAttachFiles && (canCorrectRecords || attachment.UploadedByUserId == viewerId);
}
