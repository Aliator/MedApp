using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Referrals.Responses;
using MediatR;

namespace MedApp.Application.Referrals.Commands.RedirectReferral;

/// <summary>
/// A triager redirects a referral awaiting triage, with a detail: to another specialty here
/// (<paramref name="SpecialtyId"/>: it stays awaiting triage, in that specialty's inbox, from the same date received),
/// or elsewhere (<paramref name="Elsewhere"/>, another provider: it closes). Exactly one of the two.
/// </summary>
public sealed record RedirectReferralCommand(
    Guid ReferralId,
    Guid? SpecialtyId,
    string? Elsewhere,
    string Detail,
    int ExpectedVersion,
    PathwayActor Actor)
    : IRequest<ReferralResponse>;
