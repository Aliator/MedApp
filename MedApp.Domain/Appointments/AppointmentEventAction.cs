namespace MedApp.Domain.Appointments;

/// <summary>What an appointment's history entry records. Stored as its number, so never renumber these.</summary>
public enum AppointmentEventAction
{
    Requested = 0,
    Booked = 1,
    Rescheduled = 2,
    Attended = 3,
    DidNotAttend = 4,
    Cancelled = 5,
    NotesUpdated = 6
}
