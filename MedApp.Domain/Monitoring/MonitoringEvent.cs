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

    /// <summary>An admin unlocked a finished stage.</summary>
    StageUnlocked = 6,

    /// <summary>Someone changed a pathway's target date.</summary>
    DueDateChanged = 7
}
