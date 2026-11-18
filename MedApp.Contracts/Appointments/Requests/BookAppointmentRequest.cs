namespace MedApp.Contracts.Appointments.Requests;

/// <summary>Books a Requested appointment, or moves a Booked one (another time, or another member of staff).</summary>
public sealed class BookAppointmentRequest
{
    public Guid StaffUserId { get; set; }

    public DateTime StartUtc { get; set; }

    public DateTime EndUtc { get; set; }

    /// <summary>
    /// A clinic session to book it into: the member of staff must be the session's, and the time one of its free slots
    /// exactly. The appointment then takes the session's room.
    /// </summary>
    public Guid? ClinicSessionId { get; set; }

    /// <summary>A room for an appointment outside a session (optional, clash-checked); with a session, its room or none.</summary>
    public Guid? RoomId { get; set; }

    /// <summary>
    /// What the caller is doing, by the status it last saw: "Requested" to book it, "Booked" to move it. If the
    /// appointment isn't in that status any more (someone else booked or changed it meanwhile), it's a 409, so a stale
    /// "book" can't quietly move someone else's booking.
    /// </summary>
    public string ExpectedStatus { get; set; } = string.Empty;

    /// <summary>
    /// When moving a Booked one to another time or room: text or email the patient, as they chose (only when patient
    /// messages are on).
    /// </summary>
    public bool TellPatient { get; set; }
}
