namespace MedApp.Contracts.Appointments.Requests;

public sealed class CancelAppointmentRequest
{
    /// <summary>Required, at most 500 characters.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Text or email the patient that it's cancelled, as they chose (only for a booking still to come, and only when
    /// patient messages are on). The message never gives the reason.
    /// </summary>
    public bool TellPatient { get; set; }
}
