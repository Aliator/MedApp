namespace MedApp.Contracts.Clinics.Requests;

/// <summary>
/// Changes a clinic session's room, specialty, time or slot length (the clinician stays: cancel and recreate it for
/// someone else). With appointments booked into it, only its room and specialty can change, and a new room must be free.
/// </summary>
public sealed class UpdateClinicSessionRequest
{
    public Guid RoomId { get; set; }

    public Guid SpecialtyId { get; set; }

    public DateTime StartUtc { get; set; }

    public DateTime EndUtc { get; set; }

    public int SlotMinutes { get; set; }

    /// <summary>The session's <c>lastUpdated</c> as the caller last read it: a 409 if it has changed since.</summary>
    public DateTime? ExpectedLastUpdated { get; set; }
}
