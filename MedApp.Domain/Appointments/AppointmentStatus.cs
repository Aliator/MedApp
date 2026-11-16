namespace MedApp.Domain.Appointments;

/// <summary>Where an appointment is. Stored as its number, so never renumber these.</summary>
public enum AppointmentStatus
{
    /// <summary>Needs booking: no member of staff or time yet.</summary>
    Requested = 0,

    /// <summary>With a member of staff, at a start and end time.</summary>
    Booked = 1,

    /// <summary>It happened (recorded once its start has passed).</summary>
    Attended = 2,

    /// <summary>The patient didn't come (recorded once its start has passed).</summary>
    DidNotAttend = 3,

    /// <summary>Called off, with a reason. Final.</summary>
    Cancelled = 4
}
