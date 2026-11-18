namespace MedApp.Contracts.Sites.Requests;

/// <summary>
/// A site's opening hours from today (past days keep what they had): one entry per day it's open. None puts it back on
/// the default, Monday to Friday, 08:00 to 18:00.
/// </summary>
public sealed class SiteHoursRequest
{
    /// <summary>At most one per day of the week.</summary>
    public List<SiteHoursDayRequest> Days { get; set; } = [];
}

/// <summary>When a site is open on one day of the week, at UK clock times.</summary>
public sealed class SiteHoursDayRequest
{
    /// <summary>"Monday" to "Sunday".</summary>
    public string DayOfWeek { get; set; } = string.Empty;

    /// <summary>Between 06:00 and 23:00, on a 5-minute boundary.</summary>
    public TimeOnly Opens { get; set; }

    public TimeOnly Closes { get; set; }
}
