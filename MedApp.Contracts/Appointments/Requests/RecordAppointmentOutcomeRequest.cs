namespace MedApp.Contracts.Appointments.Requests;

/// <summary>Records whether the patient came, once the appointment has started.</summary>
public sealed class RecordAppointmentOutcomeRequest
{
    /// <summary>"Attended" or "DidNotAttend".</summary>
    public string Outcome { get; set; } = string.Empty;
}
