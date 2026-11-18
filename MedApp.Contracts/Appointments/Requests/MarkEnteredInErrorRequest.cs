namespace MedApp.Contracts.Appointments.Requests;

/// <summary>Why an appointment recorded after the event is withdrawn as entered in error.</summary>
public sealed class MarkEnteredInErrorRequest
{
    /// <summary>Required, at most 500 characters.</summary>
    public string Reason { get; set; } = string.Empty;
}
