using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Referrals.Responses;
using MediatR;

namespace MedApp.Application.Referrals.Commands.EditReferral;

/// <summary>
/// Replaces a referral's details while it awaits triage, from the version the caller saw
/// (<paramref name="ExpectedVersion"/>). The history records which fields changed, never the old text.
/// </summary>
public sealed record EditReferralCommand(
    Guid ReferralId,
    Guid SpecialtyId,
    DateOnly ReceivedOn,
    string ReferrerPractice,
    string? ReferrerClinician,
    string? ReferrerOdsCode,
    string RequestedPriority,
    string Reason,
    int ExpectedVersion,
    PathwayActor Actor)
    : IRequest<ReferralResponse>;
