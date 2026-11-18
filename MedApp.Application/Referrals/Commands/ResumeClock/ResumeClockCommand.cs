using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MediatR;

namespace MedApp.Application.Referrals.Commands.ResumeClock;

/// <summary>
/// Resumes a referral's paused waiting-time clock today, from the version the caller saw: the paused days move its
/// target on, and the pathway's target date goes back to the clock's. To change a review date, resume and pause again.
/// </summary>
public sealed record ResumeClockCommand(Guid ReferralId, int ExpectedVersion, PathwayActor Actor)
    : IRequest<PathwayReferralResponse>;
