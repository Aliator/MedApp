using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Referrals;
using MedApp.Domain.Teams;

namespace MedApp.Application.Referrals.Common;

/// <summary>
/// A referral's waiting-time clock: whole UK days (<see cref="DateOnly"/>), like target dates. Pure arithmetic on the
/// dates staff entered and the team's own targets, chosen by the priority a clinician set: an administrative
/// measure, like Overdue, never a clinical one (<c>docs/clinical-safety/intended-purpose-and-device-decision.md</c>).
/// <list type="bullet">
/// <item><c>PausedDays</c>: the total of finished pauses, each <c>ResumedOn − PausedOn</c> (a pause started and ended the
/// same day counts 0).</item>
/// <item><c>WaitStartOn = ReceivedOn + PausedDays</c>, and <c>TargetOn = WaitStartOn + TargetDays</c>, both stored.</item>
/// <item><see cref="AsOf"/>: the day it stopped, else the day the open pause began, else today.</item>
/// <item><see cref="DaysWaited"/> = <c>AsOf − WaitStartOn</c>; <see cref="IsBreached"/> when <c>AsOf &gt; TargetOn</c>, so a
/// clock that breached before it was paused stays breached.</item>
/// </list>
/// The pathway accepted from the referral takes its target date from the clock (<see cref="PathwayTargetOn"/>): the
/// clock's target while it runs, the pause's review date while it's paused, and whatever it was (editable by hand
/// again) once it's stopped.
/// </summary>
public static class WaitingClock
{
    /// <summary>The <c>source</c> a target date change made by the clock carries in its audit details.</summary>
    public const string AuditSource = "clock";

    public const string Running = "Running";
    public const string Paused = "Paused";
    public const string Stopped = "Stopped";

    /// <summary>Why <c>PATCH</c> refuses a target date while the clock sets it.</summary>
    public const string TargetFromClockMessage =
        "This pathway's target date comes from its referral's waiting-time clock, so it can't be changed by hand while the clock runs or is paused. Pause the clock (for a delay the patient chose), or change the priority.";

    public const string StoppedMessage = "The waiting-time clock has stopped, so it can't be paused or resumed.";

    public const string AlreadyPausedMessage = "The waiting-time clock is already paused. Resume it first, then pause it again with the new review date.";

    public const string NotPausedMessage = "The waiting-time clock isn't paused.";

    public const string NotAcceptedMessage =
        "The waiting-time clock can only be paused once the referral has been accepted: before triage, it always runs.";

    public const string PausedMeanwhileMessage =
        "Someone else paused this clock while you had it open. Reload it to see their pause.";

    /// <summary>The target for a priority, in days, from the team's table.</summary>
    public static int TargetDaysFor(Team team, PatientPathwayPriority priority) => priority switch
    {
        PatientPathwayPriority.Urgent => team.TargetDaysUrgent,
        PatientPathwayPriority.High => team.TargetDaysHigh,
        PatientPathwayPriority.Medium => team.TargetDaysMedium,
        _ => team.TargetDaysLow
    };

    /// <summary>Starts a new referral's clock on the day it was received, with its target in days.</summary>
    public static void Start(Referral referral, int targetDays)
    {
        referral.TargetDays = targetDays;
        referral.PausedDays = 0;
        referral.PausedSinceOn = null;
        referral.PauseReviewOn = null;
        referral.StoppedOn = null;
        referral.StopReason = null;
        Recalculate(referral);
    }

    /// <summary>Works out the stored dates again from the date received, the paused days and the target.</summary>
    public static void Recalculate(Referral referral)
    {
        referral.WaitStartOn = referral.ReceivedOn.AddDays(referral.PausedDays);
        referral.TargetOn = referral.WaitStartOn.AddDays(referral.TargetDays);
    }

    public static bool IsStopped(Referral referral) => referral.StoppedOn is not null;

    public static bool IsPaused(Referral referral) => referral.StoppedOn is null && referral.PausedSinceOn is not null;

    public static bool IsRunning(Referral referral) => referral.StoppedOn is null && referral.PausedSinceOn is null;

    /// <summary>Running, Paused or Stopped.</summary>
    public static string State(Referral referral) =>
        IsStopped(referral) ? Stopped : IsPaused(referral) ? Paused : Running;

    /// <summary>The day the wait is counted to: the day it stopped, else the day the open pause began, else today.</summary>
    public static DateOnly AsOf(Referral referral, DateOnly today) =>
        referral.StoppedOn ?? referral.PausedSinceOn ?? today;

    /// <summary>Whole days waited, not counting finished pauses (never below 0).</summary>
    public static int DaysWaited(Referral referral, DateOnly today) =>
        Math.Max(0, AsOf(referral, today).DayNumber - referral.WaitStartOn.DayNumber);

    /// <summary>Past its target: the day it's counted to is after <c>TargetOn</c>.</summary>
    public static bool IsBreached(Referral referral, DateOnly today) => AsOf(referral, today) > referral.TargetOn;

    /// <summary>A paused clock whose review date has passed (hazard H-42).</summary>
    public static bool IsReviewOverdue(Referral referral, DateOnly today) =>
        IsPaused(referral) && referral.PauseReviewOn is { } review && review < today;

    /// <summary>
    /// The target date the referral's pathway should have now: the clock's <c>TargetOn</c> while it runs, the pause's
    /// review date while it's paused, or null once it's stopped (the pathway keeps what it has, and staff may edit it).
    /// </summary>
    public static DateOnly? PathwayTargetOn(Referral referral) =>
        IsStopped(referral) ? null
        : IsPaused(referral) ? referral.PauseReviewOn
        : referral.TargetOn;

    /// <summary>Whether the clock sets its pathway's target date now, so staff can't edit it: it runs or is paused.</summary>
    public static bool SetsPathwayTarget(Referral? referral) => referral is not null && !IsStopped(referral);

    /// <summary>The open pause, if the clock is paused.</summary>
    public static ReferralClockPause? OpenPause(Referral referral) =>
        referral.Pauses.FirstOrDefault(p => p.ResumedAtUtc is null);

    /// <summary>
    /// Why the clock can't be paused now, or null if it can: only an accepted referral's running clock is paused (before
    /// triage it always runs; a stopped one never changes).
    /// </summary>
    public static string? WhyCantPause(Referral referral) =>
        IsStopped(referral) ? StoppedMessage
        : referral.Status != ReferralStatus.Accepted ? NotAcceptedMessage
        : IsPaused(referral) ? AlreadyPausedMessage
        : null;

    /// <summary>Why the clock can't be resumed now, or null if it can.</summary>
    public static string? WhyCantResume(Referral referral) =>
        IsStopped(referral) ? StoppedMessage
        : !IsPaused(referral) ? NotPausedMessage
        : null;

    /// <summary>The latest review date a pause started on <paramref name="today"/> may have.</summary>
    public static DateOnly LatestReviewOn(DateOnly today) => today.AddDays(ReferralClockPause.MaxReviewDays);

    /// <summary>The stop reason for a referral closed without a pathway.</summary>
    public static ClockStopReason? StopReasonFor(ReferralStatus status) => status switch
    {
        ReferralStatus.Rejected => ClockStopReason.Rejected,
        ReferralStatus.RedirectedElsewhere => ClockStopReason.RedirectedElsewhere,
        ReferralStatus.Withdrawn => ClockStopReason.Withdrawn,
        ReferralStatus.EnteredInError => ClockStopReason.EnteredInError,
        _ => null
    };
}
