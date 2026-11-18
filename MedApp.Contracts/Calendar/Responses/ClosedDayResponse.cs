namespace MedApp.Contracts.Calendar.Responses;

/// <summary>
/// A day the clinic is closed (a bank holiday, a staff training day), everywhere or at one site. Closed everywhere,
/// nothing new can go on it. A site's closed day closes that site's rooms and the hours of staff working there.
/// </summary>
public sealed class ClosedDayResponse
{
    public Guid Id { get; set; }

    /// <summary>The UK date.</summary>
    public DateOnly Date { get; set; }

    /// <summary>What it is, e.g. "Christmas Day".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The site it closes, or null when it's closed everywhere.</summary>
    public Guid? SiteId { get; set; }

    /// <summary>The site's name, or null when it's closed everywhere.</summary>
    public string? SiteName { get; set; }

    /// <summary>Before today (UK): it can't be removed, as past days don't change.</summary>
    public bool IsPast { get; set; }

    /// <summary>
    /// What was already booked on it when it was closed (they stay, and the staff are told): filled when a day closed
    /// everywhere is added; null in a list, and for a site's closed day, which closes no one's calendar until working
    /// hours say who works at the site.
    /// </summary>
    public DayBookingsResponse? Bookings { get; set; }
}
