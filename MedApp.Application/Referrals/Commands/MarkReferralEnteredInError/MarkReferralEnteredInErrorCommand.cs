using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Referrals.Responses;
using MediatR;

namespace MedApp.Application.Referrals.Commands.MarkReferralEnteredInError;

/// <summary>
/// Marks a referral awaiting triage entered in error: registered on the wrong patient, or twice, with a detail (kept on the referral, never in its history), from the version the caller saw. It closes the
/// referral. Allowed while the patient's record is restricted, so care can be stopped safely; not once it's anonymised.
/// </summary>
public sealed record MarkReferralEnteredInErrorCommand(Guid ReferralId, string Detail, int ExpectedVersion, PathwayActor Actor)
    : IRequest<ReferralResponse>;
