namespace MedApp.Contracts.Clinics.Responses;

/// <summary>A clinic that repeats, from which sessions are made when someone asks.</summary>
public sealed class ClinicPatternResponse
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

    /// <summary>"Monday" to "Sunday".</summary>
    public string DayOfWeek { get; set; } = string.Empty;

    public TimeOnly Starts { get; set; }

    public TimeOnly Ends { get; set; }

    public int SlotMinutes { get; set; }

    public int EveryWeeks { get; set; }

    public DateOnly FromDate { get; set; }

    public DateOnly? UntilDate { get; set; }

    /// <summary>It has ended: its until date has passed.</summary>
    public bool HasEnded { get; set; }

    public DateTime LastUpdated { get; set; }
}
