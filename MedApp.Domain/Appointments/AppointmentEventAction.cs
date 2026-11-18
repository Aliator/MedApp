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
    NotesUpdated = 6,

    /// <summary>
    /// An appointment that had already happened (a walk-in, or one booked outside MedApp) was recorded after the event,
    /// with its outcome: Attended or Did not attend.
    /// </summary>
    RecordedAfterEvent = 7,

    /// <summary>An appointment recorded after the event was withdrawn as entered in error, with a reason.</summary>
    EnteredInError = 8,

    /// <summary>
    /// After a Did not attend, booking staff recorded a follow-up (<see cref="DnaFollowUpDecision"/>, with an optional
    /// detail of free text).
    /// </summary>
    DnaFollowUpRecorded = 9
}
