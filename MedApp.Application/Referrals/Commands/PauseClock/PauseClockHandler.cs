using MedApp.Application.Common.Exceptions;
using MedApp.Application.Monitoring.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.Repositories;
using MedApp.Application.Referrals.Common;
using MedApp.Application.Referrals.Repositories;
using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MedApp.Domain.Referrals;
using MediatR;

namespace MedApp.Application.Referrals.Commands.PauseClock;

/// <summary>
/// Pauses a referral's waiting-time clock. In order: 404 for an unknown referral; 409 unless it was accepted and its
/// clock runs (<see cref="WaitingClock.WhyCantPause"/>), for an anonymised record, or if the referral changed since the
/// caller's version. The pause, the referral's <c>ClockPaused</c> entry and the pathway's target date moved to the
/// review date (a <c>DetailsUpdated</c> entry with <c>"source":"clock"</c>) are saved together, through the pathway's
/// change; then the monitor hears of the target date change, as it would of a hand edit.
/// </summary>
public sealed class PauseClockHandler(
    IReferralRepository referrals,
    IPatientPathwayRepository pathways,
    IPathwayMonitor pathwayMonitor,
    TimeProvider timeProvider)
    : IRequestHandler<PauseClockCommand, PathwayReferralResponse>
{
    public async Task<PathwayReferralResponse> Handle(PauseClockCommand request, CancellationToken cancellationToken)
    {
        var referral = await referrals.GetForUpdateAsync(request.ReferralId, cancellationToken)
                       ?? throw new NotFoundException($"Referral '{request.ReferralId}' was not found.");

        if (WaitingClock.WhyCantPause(referral) is { } why)
            throw new ConflictException(why);
        if (referral.PathwayId is not { } pathwayId)
            throw new ConflictException(WaitingClock.StoppedMessage);

        // Pausing stays allowed while the record is restricted, so care can be paused safely (as putting a pathway on
        // hold is); never once it's anonymised.
        var pathway = await pathways.GetForPauseOrThrowAsync(pathwayId, cancellationToken);
        ReferralWorkflow.EnsureVersion(referral, request.ExpectedVersion);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var change = new PathwayChange(pathway, request.Actor, now);
        change.PauseClock(
            PathwayEnums.Parse<ClockPauseReason>(request.Reason),
            string.IsNullOrWhiteSpace(request.Detail) ? null : request.Detail.Trim(),
            request.ReviewOn);

        await pathways.SaveAsync(change, cancellationToken);
        await pathwayMonitor.NotifyAsync(change, cancellationToken);

        return ReferralMapper.ToPathwayReferral(referral, request.Actor, DueDates.ToUkDate(now));
    }
}
