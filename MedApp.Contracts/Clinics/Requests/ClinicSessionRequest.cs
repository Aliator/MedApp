namespace MedApp.Contracts.Clinics.Requests;

/// <summary>
/// A new clinic session: a clinician's time in a room for a specialty, cut into slots. It must be in the future, on one
/// of the clinician's working days, inside their hours and the room's site's opening hours, and clash with nothing on
/// their calendar or in the room.
/// </summary>
public sealed class ClinicSessionRequest
{
    public Guid StaffUserId { get; set; }

    public Guid RoomId { get; set; }

    public Guid SpecialtyId { get; set; }

    public DateTime StartUtc { get; set; }

    public DateTime EndUtc { get; set; }

    /// <summary>5 to 120 minutes, a multiple of 5 that divides the session exactly.</summary>
    public int SlotMinutes { get; set; }
}
