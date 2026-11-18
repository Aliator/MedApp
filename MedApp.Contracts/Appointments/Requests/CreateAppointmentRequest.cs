namespace MedApp.Contracts.Appointments.Requests;

/// <summary>
/// A new appointment for a patient, optionally for one of their pathways and one of its stages. Give a member of
/// staff, a start and an end to book it (in the future, inside the working day, clash-free), or none of them to add it
/// to Requires Appointment (Requested).
/// </summary>
public sealed class CreateAppointmentRequest
{
    public Guid PatientId { get; set; }

    public Guid? PathwayId { get; set; }

    /// <summary>A stage of <see cref="PathwayId"/>. A stage can have one Requested or Booked appointment at a time.</summary>
    public Guid? StageId { get; set; }

    public Guid? StaffUserId { get; set; }

    public DateTime? StartUtc { get; set; }

    public DateTime? EndUtc { get; set; }

    /// <summary>Optional, at most 2000 characters.</summary>
    public string? Notes { get; set; }
}
