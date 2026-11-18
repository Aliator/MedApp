using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Referrals.Responses;
using MediatR;

namespace MedApp.Application.Referrals.Commands.RejectReferral;

/// <summary>
/// A triager rejects a referral awaiting triage, for a reason (a <c>ReferralRejectionReason</c>, by name) and a detail
/// (required for Other), kept on the referral, never in its history. It closes the referral.
/// </summary>
public sealed record RejectReferralCommand(Guid ReferralId, string Reason, string? Detail, int ExpectedVersion, PathwayActor Actor)
    : IRequest<ReferralResponse>;
