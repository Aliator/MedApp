namespace MedApp.Contracts.Appointments.Requests;

/// <summary>Books a Requested appointment, or moves a Booked one (another time, or another member of staff).</summary>
public sealed class BookAppointmentRequest
{
    public Guid StaffUserId { get; set; }

    public DateTime StartUtc { get; set; }

    public DateTime EndUtc { get; set; }
}
