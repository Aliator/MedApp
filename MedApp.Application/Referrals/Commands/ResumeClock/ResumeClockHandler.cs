using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Teams;
using MedApp.Application.Monitoring.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.Repositories;
using MedApp.Application.Referrals.Common;
using MedApp.Application.Referrals.Repositories;
using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MediatR;

namespace MedApp.Application.Referrals.Commands.ResumeClock;

/// <summary>
/// Resumes a referral's paused waiting-time clock, by its own team. In order: 404 for an unknown referral, or one behind
/// the viewer's team wall (recorded); 409 for another team's (<see cref="ReferralRepositoryExtensions.GetForChangeOrThrowAsync"/>,
/// then its pathway's walls); 409 unless its clock is
/// paused (<see cref="WaitingClock.WhyCantResume"/>), for a restricted or anonymised record, or if the referral changed
/// since the caller's version. The pause's end, the referral's <c>ClockResumed</c> entry and the pathway's target date
/// moved back to the clock's are saved together; then the monitor runs.
/// </summary>
public sealed class ResumeClockHandler(
    IReferralRepository referrals,
    IPatientPathwayRepository pathways,
    IPathwayMonitor pathwayMonitor,
    ITeamBoundary boundary,
    TimeProvider timeProvider)
    : IRequestHandler<ResumeClockCommand, PathwayReferralResponse>
{
    public async Task<PathwayReferralResponse> Handle(ResumeClockCommand request, CancellationToken cancellationToken)
    {
        var teams = request.Actor.Teams;
        var referral = await referrals.GetForChangeOrThrowAsync(request.ReferralId, boundary, teams, cancellationToken);

        if (WaitingClock.WhyCantResume(referral) is { } why)
            throw new ConflictException(why);
        if (referral.PathwayId is not { } pathwayId)
            throw new ConflictException(WaitingClock.StoppedMessage);

        var pathway = await pathways.GetForUpdateOrThrowAsync(pathwayId, boundary, teams, cancellationToken);
        ReferralWorkflow.EnsureVersion(referral, request.ExpectedVersion);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var change = new PathwayChange(pathway, request.Actor, now);
        change.ResumeClock();

        await pathways.SaveAsync(change, cancellationToken);
        await pathwayMonitor.NotifyAsync(change, cancellationToken);

        return ReferralMapper.ToPathwayReferral(referral, request.Actor, DueDates.ToUkDate(now));
    }
}
