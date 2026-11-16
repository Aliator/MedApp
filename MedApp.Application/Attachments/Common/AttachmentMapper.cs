using MedApp.Contracts.Attachments.Responses;
using MedApp.Domain.Attachments;

namespace MedApp.Application.Attachments.Common;

/// <summary>Attachments as the API returns them, with what the person asking may do with each.</summary>
public static class AttachmentMapper
{
    /// <summary>One attachment, with its procedure's title and stage's name now (null if none).</summary>
    public static AttachmentResponse ToResponse(AttachmentRow row, Guid viewerId, bool isAdmin)
    {
        var attachment = row.Attachment;
        return new AttachmentResponse
        {
            Id = attachment.Id,
            PatientId = attachment.PatientId,
            AttachedTo = AttachmentTarget.KindOf(attachment).ToString(),
            ProcedureId = attachment.ProcedureId,
            ProcedureTitle = attachment.ProcedureId is null ? null : row.ProcedureTitle,
            StageId = attachment.StageId,
            StageName = attachment.StageId is null ? null : row.StageName,
            FileName = attachment.FileName,
            ContentType = attachment.ContentType,
            TypeName = AttachmentFileTypes.FromContentType(attachment.ContentType)?.Name ?? attachment.ContentType,
            SizeBytes = attachment.SizeBytes,
            Sha256 = attachment.Sha256,
            ScanStatus = attachment.ScanStatus.ToString(),
            HasThumbnail = attachment.HasThumbnail && !attachment.IsDeleted,
            UploadedByUsername = attachment.UploadedByUsername,
            UploadedAtUtc = attachment.UploadedAtUtc,
            CanDelete = CanDelete(attachment, viewerId, isAdmin),
            IsDeleted = attachment.IsDeleted,
            DeletedAtUtc = attachment.DeletedAtUtc,
            DeletedByUsername = attachment.DeletedByUsername,
        };
    }

    /// <summary>One attachment and what it's attached to.</summary>
    public static AttachmentResponse ToResponse(Attachment attachment, AttachmentTarget target, Guid viewerId, bool isAdmin) =>
        ToResponse(new AttachmentRow(attachment, target.Procedure?.Title, target.Stage?.Name), viewerId, isAdmin);

    /// <summary>A list, newest first, with how many there are in all and the limits on new files.</summary>
    public static AttachmentListResponse ToListResponse(
        IReadOnlyList<AttachmentRow> rows, int totalCount, Guid viewerId, bool isAdmin) => new()
    {
        Items = rows.Select(row => ToResponse(row, viewerId, isAdmin)).ToList(),
        TotalCount = totalCount,
        MaxFileBytes = AttachmentLimits.MaxFileBytes,
        AllowedExtensions = AttachmentFileTypes.Extensions.ToList(),
    };

    /// <summary>
    /// Whether someone may delete a file: admins any, staff their own. Whether the patient, procedure or stage allows
    /// it now is checked when they try (<see cref="AttachmentTarget.WhyFilesCantChange"/>).
    /// </summary>
    public static bool CanDelete(Attachment attachment, Guid viewerId, bool isAdmin) =>
        !attachment.IsDeleted && (isAdmin || attachment.UploadedByUserId == viewerId);
}
