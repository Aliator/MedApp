namespace MedApp.Contracts.Appointments.Requests;

/// <summary>Books a Requested appointment, or moves a Booked one (another time, or another member of staff).</summary>
public sealed class BookAppointmentRequest
{
    public Guid StaffUserId { get; set; }

    public DateTime StartUtc { get; set; }

    public DateTime EndUtc { get; set; }

    /// <summary>
    /// What the caller is doing, by the status it last saw: "Requested" to book it, "Booked" to move it. If the
    /// appointment isn't in that status any more (someone else booked or changed it meanwhile), it's a 409, so a stale
    /// "book" can't quietly move someone else's booking.
    /// </summary>
    public string ExpectedStatus { get; set; } = string.Empty;
}
