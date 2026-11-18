namespace MedApp.Contracts.Clinics.Requests;

/// <summary>Clinic sessions from one UK date to another (at most 62 days), optionally narrowed.</summary>
public sealed class SearchClinicSessionsRequest
{
    public DateOnly From { get; set; }

    public DateOnly To { get; set; }

    public Guid? StaffUserId { get; set; }

    public Guid? SiteId { get; set; }

    public Guid? RoomId { get; set; }

    public Guid? SpecialtyId { get; set; }

    /// <summary>Cancelled sessions too (left out by default).</summary>
    public bool IncludeCancelled { get; set; }

    /// <summary>Only sessions with a slot still free and not yet started (for booking).</summary>
    public bool WithFreeSlots { get; set; }
}
