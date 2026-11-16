namespace MedApp.Contracts.Appointments.Requests;

public sealed class UpdateAppointmentNotesRequest
{
    /// <summary>At most 2000 characters.</summary>
    public string Notes { get; set; } = string.Empty;
}
