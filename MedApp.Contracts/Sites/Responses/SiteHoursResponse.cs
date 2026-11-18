namespace MedApp.Contracts.Sites.Responses;

/// <summary>A site's opening hours today.</summary>
public sealed class SiteHoursResponse
{
    public Guid SiteId { get; set; }

    /// <summary>It has hours of its own; otherwise it's open the default (the days given).</summary>
    public bool HasOwnHours { get; set; }

    /// <summary>The UK date its own hours have applied from, if it has any.</summary>
    public DateOnly? Since { get; set; }

    /// <summary>The days it's open, Monday first.</summary>
    public List<SiteHoursDayResponse> Days { get; set; } = [];
}

public sealed class SiteHoursDayResponse
{
    /// <summary>"Monday" to "Sunday".</summary>
    public string DayOfWeek { get; set; } = string.Empty;

    public TimeOnly Opens { get; set; }

    public TimeOnly Closes { get; set; }
}
