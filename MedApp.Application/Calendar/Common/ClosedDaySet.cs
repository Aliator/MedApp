using MedApp.Domain.Calendar;

namespace MedApp.Application.Calendar.Common;

/// <summary>
/// The days the clinic is closed over some range of dates, by UK date, with their names (<see cref="ClosedDay"/>).
/// A day closed everywhere is like a weekend for everything new (<see cref="WorkingHours.IsWorkingDay(DateOnly, ClosedDaySet)"/>):
/// <see cref="IsClosed"/> and <see cref="NameOn"/> are about those. A site's closed day (<see cref="ClosedDay.SiteId"/>)
/// closes only that site (<see cref="IsClosedAt"/>, <see cref="NameAt"/>), which nobody's calendar follows until
/// working hours say who works where; so it's kept apart and never closes a calendar on its own.
/// </summary>
public sealed class ClosedDaySet
{
    private readonly IReadOnlyDictionary<DateOnly, string> _names;
    private readonly IReadOnlyDictionary<(DateOnly Date, Guid SiteId), string> _siteNames;

    /// <summary>Closed days everywhere and at sites, as stored.</summary>
    public ClosedDaySet(IEnumerable<ClosedDay> days)
    {
        var list = days.ToList();
        _names = ByDate(list.Where(d => d.SiteId is null).Select(d => (d.Date, d.Name)));
        _siteNames = list
            .Where(d => d.SiteId is not null)
            .GroupBy(d => (d.Date, SiteId: d.SiteId!.Value))
            .ToDictionary(g => g.Key, g => g.First().Name);
    }

    /// <summary>Days closed everywhere.</summary>
    public ClosedDaySet(IEnumerable<(DateOnly Date, string Name)> days)
    {
        _names = ByDate(days);
        _siteNames = new Dictionary<(DateOnly, Guid), string>();
    }

    /// <summary>No closed days.</summary>
    public static ClosedDaySet None { get; } = new(Array.Empty<(DateOnly, string)>());

    /// <summary>The dates closed everywhere.</summary>
    public IEnumerable<DateOnly> Dates => _names.Keys;

    /// <summary>Whether the date is closed everywhere.</summary>
    public bool IsClosed(DateOnly date) => _names.ContainsKey(date);

    /// <summary>The name of the day closed everywhere on a date ("Christmas Day"), or null if the clinic is open.</summary>
    public string? NameOn(DateOnly date) => _names.GetValueOrDefault(date);

    /// <summary>Whether a site is closed on a date: closed everywhere, or that site closed.</summary>
    public bool IsClosedAt(DateOnly date, Guid siteId) => NameAt(date, siteId) is not null;

    /// <summary>Why a site is closed on a date (the day closed everywhere first, then the site's own), or null if it's open.</summary>
    public string? NameAt(DateOnly date, Guid siteId) =>
        NameOn(date) ?? _siteNames.GetValueOrDefault((date, siteId));

    private static Dictionary<DateOnly, string> ByDate(IEnumerable<(DateOnly Date, string Name)> days) =>
        days.GroupBy(d => d.Date).ToDictionary(g => g.Key, g => g.First().Name);
}
