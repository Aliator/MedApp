using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Referrals.Responses;
using MediatR;

namespace MedApp.Application.Referrals.Commands.WithdrawReferral;

/// <summary>
/// Withdraws a referral awaiting triage: the referrer or the patient withdrew it, with a detail (kept on the referral, never in its history), from the version the caller saw. It closes the
/// referral. Allowed while the patient's record is restricted, so care can be stopped safely; not once it's anonymised.
/// </summary>
public sealed record WithdrawReferralCommand(Guid ReferralId, string Detail, int ExpectedVersion, PathwayActor Actor)
    : IRequest<ReferralResponse>;
