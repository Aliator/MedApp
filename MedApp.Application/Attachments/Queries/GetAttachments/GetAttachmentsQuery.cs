using MedApp.Application.Common.Teams;
using MedApp.Contracts.Attachments.Responses;
using MediatR;

namespace MedApp.Application.Attachments.Queries.GetAttachments;

/// <summary>
/// The files attached to exactly one of: a patient (everything attached to them, anywhere), a pathway (its own, not
/// its stages'), a stage or a referral (its letter). Newest first, at most <c>AttachmentLimits.MaxListed</c>. Deleted
/// files only for a holder of <c>CorrectRecords</c> who asks (<paramref name="IncludeDeleted"/>); nobody else sees them.
/// Each file's <c>CanDelete</c> follows the viewer's permissions (<paramref name="CanCorrectRecords"/>,
/// <paramref name="CanAttachFiles"/>). The team walls (<paramref name="Scope"/>, from the request; null only outside one,
/// which is unscoped): a pathway, stage or referral behind them is a 404, recorded; a patient's list holds only the files
/// on pathways the viewer sees in full, or of their own team.
/// </summary>
public sealed record GetAttachmentsQuery(
    Guid? PatientId,
    Guid? PathwayId,
    Guid? StageId,
    Guid? ReferralId,
    bool IncludeDeleted,
    Guid ViewerId,
    bool CanCorrectRecords,
    bool CanAttachFiles,
    TeamScope? Scope = null)
    : IRequest<AttachmentListResponse>
{
    /// <summary>The viewer's team walls: the request's, or the system's outside a request.</summary>
    public TeamScope Teams => Scope ?? TeamScope.System;
}
