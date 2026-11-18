namespace MedApp.Contracts.Appointments.Requests;

/// <summary>
/// An appointment that has already happened (a walk-in, or one booked outside MedApp), recorded after the event with
/// its outcome. Its start must have passed, on a working day in the last 30 days, inside the working day, on 5-minute
/// boundaries, and not overlap another of the member of staff's appointments.
/// </summary>
public sealed class RecordPastAppointmentRequest
{
    public Guid PatientId { get; set; }

    public Guid? ProcedureId { get; set; }

    /// <summary>
    /// A stage of <see cref="ProcedureId"/>. If the stage has a visit in Needs booking, that appointment is the one
    /// recorded; one already booked is a 409 (record its outcome, or cancel it, instead).
    /// </summary>
    public Guid? StageId { get; set; }

    public Guid StaffUserId { get; set; }

    public DateTime StartUtc { get; set; }

    public DateTime EndUtc { get; set; }

    /// <summary>Attended or DidNotAttend.</summary>
    public string Outcome { get; set; } = string.Empty;

    /// <summary>Optional, at most 2000 characters.</summary>
    public string? Notes { get; set; }
}
