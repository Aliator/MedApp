namespace MedApp.Contracts.Calendar.Responses;

/// <summary>A member of staff's working hours today.</summary>
public sealed class StaffHoursResponse
{
    public Guid StaffUserId { get; set; }

    /// <summary>They have hours of their own; otherwise they work the default (the days given).</summary>
    public bool HasOwnHours { get; set; }

    /// <summary>The UK date their own hours have applied from, if they have any.</summary>
    public DateOnly? Since { get; set; }

    /// <summary>Whether the signed-in user can change them.</summary>
    public bool CanEdit { get; set; }

    /// <summary>The days they work, Monday first.</summary>
    public List<StaffHoursDayResponse> Days { get; set; } = [];
}

public sealed class StaffHoursDayResponse
{
    /// <summary>"Monday" to "Sunday".</summary>
    public string DayOfWeek { get; set; } = string.Empty;

    public TimeOnly Starts { get; set; }

    public TimeOnly Ends { get; set; }

    /// <summary>Null on the default (no particular site).</summary>
    public Guid? SiteId { get; set; }

    public string? SiteName { get; set; }
}
