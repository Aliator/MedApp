namespace MedApp.Contracts.Calendar.Requests;

/// <summary>
/// A member of staff's working hours from today (past days keep what they had): one entry per day they work. None puts
/// them back on the default, Monday to Friday, 08:00 to 18:00.
/// </summary>
public sealed class StaffHoursRequest
{
    /// <summary>At most one per day of the week.</summary>
    public List<StaffHoursDayRequest> Days { get; set; } = [];
}

/// <summary>The hours someone works on one day of the week, at UK clock times, and where.</summary>
public sealed class StaffHoursDayRequest
{
    /// <summary>"Monday" to "Sunday".</summary>
    public string DayOfWeek { get; set; } = string.Empty;

    /// <summary>Between 06:00 and 23:00, on a 5-minute boundary, inside the site's opening hours that day.</summary>
    public TimeOnly Starts { get; set; }

    public TimeOnly Ends { get; set; }

    /// <summary>A site that isn't archived (or the one they already work at that day).</summary>
    public Guid SiteId { get; set; }
}
