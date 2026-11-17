namespace MedApp.Domain.Notifications;

/// <summary>
/// What a notification is about. Stored as its number, so never renumber these. 1 to 9 are kept for the calendar's
/// kinds; the monitoring kinds (one of an admin's rules matched) are 10 to 17, one for each <c>MonitoringEvent</c>;
/// 18 to 20 are about requests to delete a procedure's note.
/// </summary>
public enum NotificationKind
{
    /// <summary>Someone assigned the user to a procedure.</summary>
    ProcedureAssigned = 0,

    /// <summary>Someone booked an appointment with the user, or moved one to them.</summary>
    AppointmentBooked = 1,

    /// <summary>Someone moved one of the user's appointments to another time.</summary>
    AppointmentRescheduled = 2,

    /// <summary>Someone cancelled one of the user's appointments, or moved it to someone else.</summary>
    AppointmentCancelled = 3,

    /// <summary>An admin invited the user to a meeting, or added them to one.</summary>
    MeetingInvited = 4,

    /// <summary>An admin moved a meeting the user is going to another time.</summary>
    MeetingChanged = 5,

    /// <summary>An admin cancelled a meeting the user was going to, or took them off it.</summary>
    MeetingCancelled = 6,

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
    MonitoredDueDateChanged = 17,

    /// <summary>A member of staff asked for a note to be deleted (sent to every admin but them).</summary>
    NoteDeletionRequested = 18,

    /// <summary>An admin deleted the note the user asked to be deleted.</summary>
    NoteDeletionApproved = 19,

    /// <summary>An admin kept the note the user asked to be deleted.</summary>
    NoteDeletionDeclined = 20
}
