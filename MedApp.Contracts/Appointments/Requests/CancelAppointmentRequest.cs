namespace MedApp.Contracts.Appointments.Requests;

public sealed class CancelAppointmentRequest
{
    /// <summary>Required, at most 500 characters.</summary>
    public string Reason { get; set; } = string.Empty;
}
