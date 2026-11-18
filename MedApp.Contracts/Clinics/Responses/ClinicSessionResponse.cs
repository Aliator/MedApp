namespace MedApp.Contracts.Clinics.Responses;

/// <summary>A clinic session, with how many of its slots are booked and free. Names no patient.</summary>
public sealed class ClinicSessionResponse
{
    public Guid Id { get; set; }

    public Guid StaffUserId { get; set; }

    public string StaffUsername { get; set; } = string.Empty;

    public Guid RoomId { get; set; }

    public string RoomName { get; set; } = string.Empty;

    public Guid SiteId { get; set; }

    public string SiteName { get; set; } = string.Empty;

    public Guid SpecialtyId { get; set; }

    public string SpecialtyName { get; set; } = string.Empty;

    /// <summary>The pattern that made it, if any.</summary>
    public Guid? PatternId { get; set; }

    /// <summary>Its UK date.</summary>
    public DateOnly Date { get; set; }

    public DateTime StartUtc { get; set; }

    public DateTime EndUtc { get; set; }

    public int SlotMinutes { get; set; }

    public int SlotCount { get; set; }

    /// <summary>Slots with an appointment on the calendar (Booked, Attended or Did not attend).</summary>
    public int BookedCount { get; set; }

    /// <summary>Slots with no appointment that haven't started: what can still be booked.</summary>
    public int FreeSlotCount { get; set; }

    /// <summary>The start of each of those free slots, in order: what the booking dialog offers.</summary>
    public List<DateTime> FreeSlotStartsUtc { get; set; } = [];

    /// <summary>Planned or Cancelled.</summary>
    public string Status { get; set; } = string.Empty;

    public string? CancelReason { get; set; }

    public DateTime? CancelledAtUtc { get; set; }

    public DateTime LastUpdated { get; set; }

    /// <summary>The caller may change or cancel it: they manage clinics, it's Planned and not on a past day.</summary>
    public bool CanChange { get; set; }
}
