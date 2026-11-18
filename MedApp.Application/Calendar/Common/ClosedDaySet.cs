using MedApp.Domain.Calendar;

namespace MedApp.Application.Calendar.Common;

/// <summary>
/// The days the clinic is closed over some range of dates, by UK date, with their names (<see cref="ClosedDay"/>). A
/// closed day is like a weekend for everything new (<see cref="WorkingHours.IsWorkingDay(DateOnly, ClosedDaySet)"/>).
/// </summary>
public sealed class ClosedDaySet
{
    private readonly IReadOnlyDictionary<DateOnly, string> _names;

    public ClosedDaySet(IEnumerable<ClosedDay> days) : this(days.Select(d => (d.Date, d.Name)))
    {
    }

    public ClosedDaySet(IEnumerable<(DateOnly Date, string Name)> days) =>
        _names = days.GroupBy(d => d.Date).ToDictionary(g => g.Key, g => g.First().Name);

    /// <summary>No closed days.</summary>
    public static ClosedDaySet None { get; } = new(Array.Empty<(DateOnly, string)>());

    /// <summary>The closed days' dates.</summary>
    public IEnumerable<DateOnly> Dates => _names.Keys;

    public bool IsClosed(DateOnly date) => _names.ContainsKey(date);

    /// <summary>The closed day's name on a date ("Christmas Day"), or null if the clinic is open.</summary>
    public string? NameOn(DateOnly date) => _names.GetValueOrDefault(date);
}
