namespace MedApp.Domain.Monitoring;

/// <summary>
/// What a monitoring rule watches for: a change to a procedure, as its audit entries record it. Stored as its number,
/// so never renumber these.
/// </summary>
public enum MonitoringEvent
{
    /// <summary>Someone created a procedure.</summary>
    ProcedureCreated = 0,

    /// <summary>Someone finished a stage.</summary>
    StageFinished = 1,

    /// <summary>Someone made a procedure Completed.</summary>
    ProcedureCompleted = 2,

    /// <summary>Someone made a procedure Blocked.</summary>
    ProcedureBlocked = 3,

    /// <summary>Someone made a procedure Cancelled.</summary>
    ProcedureCancelled = 4,

    /// <summary>
    /// Someone was assigned to a procedure. The person a rule names is the one assigned (assigning is for admins, so
    /// this is how to follow a staff member's new work).
    /// </summary>
    ProcedureAssigned = 5,

    /// <summary>An admin unlocked a finished stage.</summary>
    StageUnlocked = 6,

    /// <summary>Someone changed a procedure's due date.</summary>
    DueDateChanged = 7
}
