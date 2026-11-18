using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MediatR;

namespace MedApp.Application.Referrals.Commands.PauseClock;

/// <summary>
/// Pauses an accepted referral's waiting-time clock today, for a delay the patient chose or caused (a
/// <c>ClockPauseReason</c> by name, with a detail, required for Other), until a review date from tomorrow to 90 days
/// ahead, from the version the caller saw. The pathway's target date becomes the review date. Allowed while the
/// patient's record is restricted, so care can be paused safely; not once it's anonymised.
/// </summary>
public sealed record PauseClockCommand(
    Guid ReferralId,
    string Reason,
    string? Detail,
    DateOnly ReviewOn,
    int ExpectedVersion,
    PathwayActor Actor)
    : IRequest<PathwayReferralResponse>;
