namespace MedApp.Domain.Notifications;

/// <summary>
/// What a notification is about. Stored as its number, so never renumber these. 1 to 9 are kept for the calendar's
/// kinds; the monitoring kinds (one of an admin's rules matched) start at 10, one for each <c>MonitoringEvent</c>.
/// </summary>
public enum NotificationKind
{
    /// <summary>Someone assigned the user to a procedure.</summary>
    ProcedureAssigned = 0,

    /// <summary>Monitoring: someone created a procedure.</summary>
    MonitoredProcedureCreated = 10,

    /// <summary>Monitoring: someone finished a stage.</summary>
    MonitoredStageFinished = 11,

    /// <summary>Monitoring: someone completed a procedure.</summary>
    MonitoredProcedureCompleted = 12,

    /// <summary>Monitoring: someone blocked a procedure.</summary>
    MonitoredProcedureBlocked = 13,

    /// <summary>Monitoring: someone cancelled a procedure.</summary>
    MonitoredProcedureCancelled = 14,

    /// <summary>Monitoring: someone was assigned to a procedure.</summary>
    MonitoredProcedureAssigned = 15,

    /// <summary>Monitoring: an admin unlocked a stage.</summary>
    MonitoredStageUnlocked = 16,

    /// <summary>Monitoring: someone changed a procedure's due date.</summary>
    MonitoredDueDateChanged = 17
}
