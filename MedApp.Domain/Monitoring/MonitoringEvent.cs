namespace MedApp.Domain.Monitoring;

/// <summary>
/// What a monitoring rule watches for: a change to a pathway, as its audit entries record it. Stored as its number,
/// so never renumber these.
/// </summary>
public enum MonitoringEvent
{
    /// <summary>Someone created a pathway.</summary>
    PathwayCreated = 0,

    /// <summary>Someone finished a stage.</summary>
    StageFinished = 1,

    /// <summary>Someone made a pathway Completed.</summary>
    PathwayCompleted = 2,

    /// <summary>Someone made a pathway Blocked.</summary>
    PathwayBlocked = 3,

    /// <summary>Someone made a pathway Cancelled.</summary>
    PathwayCancelled = 4,

    /// <summary>
    /// Someone was assigned to a pathway. The person a rule names is the one assigned (assigning is for admins, so
    /// this is how to follow a staff member's new work).
    /// </summary>
    PathwayAssigned = 5,

    /// <summary>Someone unlocked a finished stage (a holder of UnlockStages).</summary>
    StageUnlocked = 6,

    /// <summary>Someone changed a pathway's target date.</summary>
    DueDateChanged = 7,

    // 8 to 13 are the service alerts: time-based, found by the service-alerts job against a rule's ThresholdDays
    // rather than fired by a change (docs/centre-phases-3-5-plan.md, decisions 134 and 135).

    /// <summary>A referral has awaited triage longer than the rule's days (from the day it was received).</summary>
    ReferralNotTriaged = 8,

    /// <summary>A result has awaited sign-off longer than the rule's days (from when it was reported).</summary>
    ResultNotSignedOff = 9,

    /// <summary>A visit has been Requested (Requires appointment) longer than the rule's days.</summary>
    VisitNotBooked = 10,

    /// <summary>A specimen is still open more than the rule's days past its type's expected turnaround.</summary>
    SpecimenOverdue = 11,

    /// <summary>A referral's running waiting-time clock is within the rule's days of its target (0: past it).</summary>
    WaitBreached = 12,

    /// <summary>A paused waiting-time clock's review date passed more than the rule's days ago.</summary>
    PauseReviewPassed = 13,

    /// <summary>
    /// Someone finished a stage whose visit is Required without an attended one (a holder of FinishWithoutVisit, with a
    /// reason). The same finish is a <see cref="StageFinished"/> too.
    /// </summary>
    StageFinishedWithoutVisit = 14,

    // 15 (HandoverNotPickedUp) is reserved for MC7's monitoring.

    /// <summary>An open lab case has been at risk of missing its due date (MC19's strict clock) more than the rule's days.</summary>
    LabCaseAtRisk = 16,

    /// <summary>An open lab case is more than the rule's days past its due date (0: past it).</summary>
    LabCaseOverdue = 17
}
