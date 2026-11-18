using MedApp.Contracts.Attachments.Responses;
using MediatR;

namespace MedApp.Application.Attachments.Queries.GetAttachments;

/// <summary>
/// The files attached to exactly one of: a patient (everything attached to them, anywhere), a pathway (its own, not
/// its stages') or a stage. Newest first, at most <c>AttachmentLimits.MaxListed</c>. Deleted files only for a holder of
/// <c>CorrectRecords</c> who asks (<paramref name="IncludeDeleted"/>); nobody else sees them. Each file's
/// <c>CanDelete</c> follows the viewer's permissions (<paramref name="CanCorrectRecords"/>,
/// <paramref name="CanAttachFiles"/>).
/// </summary>
public sealed record GetAttachmentsQuery(
    Guid? PatientId,
    Guid? PathwayId,
    Guid? StageId,
    bool IncludeDeleted,
    Guid ViewerId,
    bool CanCorrectRecords,
    bool CanAttachFiles)
    : IRequest<AttachmentListResponse>;
