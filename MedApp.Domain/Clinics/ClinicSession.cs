using MedApp.Domain.Sites;
using MedApp.Domain.Specialties;

namespace MedApp.Domain.Clinics;

/// <summary>
/// A block of a clinician's time in a room for a specialty, cut into slots of <see cref="SlotMinutes"/> (centre plan
/// decision 123): busy time on their calendar for blocks and meetings, and where appointments go, one patient per slot.
/// A room holds one session at a time. Made one at a time, or by a <see cref="ClinicPattern"/> when someone asks.
/// Cancelled rather than deleted; its details are about staff, never patients.
/// </summary>
public sealed class ClinicSession
{
    public Guid Id { get; init; }

    /// <summary>The clinician. No foreign key to the users table, as with other staff IDs.</summary>
    public Guid StaffUserId { get; init; }

    public Guid RoomId { get; set; }
    public Room Room { get; set; } = null!;

    public Guid SpecialtyId { get; set; }
    public Specialty Specialty { get; set; } = null!;

    /// <summary>The pattern that made it, if any: a pattern makes at most one session on a date.</summary>
    public Guid? PatternId { get; init; }

    /// <summary>Its UK date (the day of <see cref="StartUtc"/>), for the pattern's one-a-day rule.</summary>
    public DateOnly Date { get; set; }

    public DateTime StartUtc { get; set; }

    public DateTime EndUtc { get; set; }

    /// <summary>5 to 120 minutes, dividing the session exactly.</summary>
    public int SlotMinutes { get; set; }

    public ClinicSessionStatus Status { get; set; }

    public DateTime CreatedAtUtc { get; init; }

    public Guid CreatedByUserId { get; init; }

    public DateTime? CancelledAtUtc { get; set; }

    public Guid? CancelledByUserId { get; set; }

    /// <summary>Why it was cancelled (about the clinic, never a patient), once it is.</summary>
    public string? CancelReason { get; set; }

    public DateTime LastUpdated { get; set; }

    /// <summary>Changes on every save, so the later of two overlapping changes is refused (a 409).</summary>
    public byte[] RowVersion { get; set; } = [];
}
