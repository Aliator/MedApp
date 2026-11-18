namespace MedApp.Contracts.Clinics.Requests;

/// <summary>Cancels a clinic session, and every appointment booked into it, with a reason.</summary>
public sealed class CancelClinicSessionRequest
{
    /// <summary>Why (about the clinic, never a patient): at most 500 characters. Each appointment is cancelled with it.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>The session's <c>lastUpdated</c> as the caller last read it: a 409 if it has changed since.</summary>
    public DateTime? ExpectedLastUpdated { get; set; }

    /// <summary>Text or email each patient whose appointment is cancelled, as they chose (only when patient messages are on).</summary>
    public bool TellPatients { get; set; }
}
