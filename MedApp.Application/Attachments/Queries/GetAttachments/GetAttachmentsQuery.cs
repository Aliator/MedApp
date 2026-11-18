using MedApp.Contracts.Attachments.Responses;
using MediatR;

namespace MedApp.Application.Attachments.Queries.GetAttachments;

/// <summary>
/// The files attached to exactly one of: a patient (everything attached to them, anywhere), a pathway (its own, not
/// its stages'), a stage or a referral (its letter). Newest first, at most <c>AttachmentLimits.MaxListed</c>. Deleted files only for an admin
/// who asks (<paramref name="IncludeDeleted"/>); staff never see them.
/// </summary>
public sealed record GetAttachmentsQuery(
    Guid? PatientId,
    Guid? PathwayId,
    Guid? StageId,
    Guid? ReferralId,
    bool IncludeDeleted,
    Guid ViewerId,
    bool CanCorrectRecords)
    : IRequest<AttachmentListResponse>;
