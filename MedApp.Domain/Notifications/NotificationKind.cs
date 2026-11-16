namespace MedApp.Domain.Notifications;

/// <summary>What a notification is about. Stored as its number, so never renumber these.</summary>
public enum NotificationKind
{
    /// <summary>Someone assigned the user to a procedure.</summary>
    ProcedureAssigned = 0,

    /// <summary>Someone booked an appointment with the user, or moved one to them.</summary>
    AppointmentBooked = 1,

    /// <summary>Someone moved one of the user's appointments to another time.</summary>
    AppointmentRescheduled = 2,

    /// <summary>Someone cancelled one of the user's appointments, or moved it to someone else.</summary>
    AppointmentCancelled = 3
}
