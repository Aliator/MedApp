namespace MedApp.Contracts.Referrals.Responses;

/// <summary>
/// A referral's waiting-time clock, in whole UK days: dates staff entered and the team's own target for the
/// priority a clinician set. Administrative, like Overdue: never ranked by anything clinical.
/// </summary>
public sealed class WaitingClockResponse
{
    /// <summary>Running, Paused or Stopped.</summary>
    public string State { get; set; } = string.Empty;

    /// <summary>The target in days ("Day 84 of 126").</summary>
    public int TargetDays { get; set; }

    /// <summary>The date received plus every finished pause's days: the day the wait counts from.</summary>
    public DateOnly WaitStartOn { get; set; }

    /// <summary>Breached once the wait goes past this day.</summary>
    public DateOnly TargetOn { get; set; }

    /// <summary>Whole days waited: to today while it runs, to the pause's start while paused, or to when it stopped.</summary>
    public int DaysWaited { get; set; }

    /// <summary>The total of finished pauses, in days.</summary>
    public int PausedDays { get; set; }

    /// <summary>Past its target (a clock that breached before a pause stays breached).</summary>
    public bool IsBreached { get; set; }

    /// <summary>Paused: since when.</summary>
    public DateOnly? PausedSinceOn { get; set; }

    /// <summary>Paused: the review date, which is the pathway's target date while it lasts.</summary>
    public DateOnly? PauseReviewOn { get; set; }

    /// <summary>Paused, and its review date has passed.</summary>
    public bool IsReviewOverdue { get; set; }

    /// <summary>Stopped: when.</summary>
    public DateOnly? StoppedOn { get; set; }

    /// <summary>
    /// Stopped: why (StopStageFinished, PathwayDischarged, PathwayCancelled, Rejected, RedirectedElsewhere, Withdrawn or
    /// EnteredInError).
    /// </summary>
    public string? StopReason { get; set; }
}
