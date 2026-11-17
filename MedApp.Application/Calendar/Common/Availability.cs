namespace MedApp.Application.Calendar.Common;

/// <summary>A stretch of time, in UTC.</summary>
public sealed record TimeRange(DateTime StartUtc, DateTime EndUtc);

/// <summary>The times everyone asked about is free on one UK date.</summary>
public sealed record FreeDay(DateOnly Date, IReadOnlyList<TimeRange> Free);

/// <summary>
/// When several people are all free, for arranging a meeting: plain logic over their calendars as the repository loads
/// them (<see cref="CalendarData"/>), so it's the same busy time a clash check sees (<see cref="CalendarClashes"/>):
/// appointments, stored and template blocks (with day overrides and exceptions), and meetings.
/// </summary>
public static class Availability
{
    public const int MaxSuggestions = 10;
    public const int MaxSuggestionsPerDay = 3;
    public const int SuggestionStepMinutes = 15;

    /// <summary>
    /// For each working day from <paramref name="from"/> to <paramref name="to"/>, the times everyone is free: inside
    /// the working day, after <paramref name="nowUtc"/>, rounded inwards to 5-minute boundaries. A day with no free time
    /// is still listed, with none. Each person's data must cover the range.
    /// </summary>
    /// <param name="exceptMeetingId">A meeting being moved, which doesn't count as busy.</param>
    public static IReadOnlyList<FreeDay> FreeTime(
        IReadOnlyCollection<CalendarData> people,
        DateOnly from,
        DateOnly to,
        DateTime nowUtc,
        Guid? exceptMeetingId = null)
    {
        var busy = people
            .SelectMany(p => CalendarClashes.BusyTimes(p, exceptMeetingId))
            .Select(c => new TimeRange(c.StartUtc, c.EndUtc))
            .OrderBy(r => r.StartUtc)
            .ToList();

        var days = new List<FreeDay>();
        for (var date = from; date <= to; date = date.AddDays(1))
        {
            if (!WorkingHours.IsWorkingDay(date))
                continue;

            var dayStart = WorkingHours.DayStartUtc(date);
            var dayEnd = WorkingHours.DayEndUtc(date);
            var start = nowUtc > dayStart ? nowUtc : dayStart;
            days.Add(new FreeDay(date, Subtract(new TimeRange(start, dayEnd), busy)));
        }

        return days;
    }

    /// <summary>
    /// Up to <see cref="MaxSuggestions"/> start times when everyone is free for <paramref name="durationMinutes"/>,
    /// earliest first, on <see cref="SuggestionStepMinutes"/>-minute steps of the UK clock, at most
    /// <see cref="MaxSuggestionsPerDay"/> a day (so they spread across days), and none overlapping the one before.
    /// </summary>
    public static IReadOnlyList<TimeRange> Suggestions(IEnumerable<FreeDay> days, int durationMinutes)
    {
        var length = TimeSpan.FromMinutes(durationMinutes);
        var suggestions = new List<TimeRange>();

        foreach (var day in days.OrderBy(d => d.Date))
        {
            var today = 0;
            foreach (var free in day.Free)
            {
                for (var start = RoundUp(free.StartUtc, SuggestionStepMinutes);
                     start + length <= free.EndUtc && today < MaxSuggestionsPerDay;
                     start = RoundUp(start + length, SuggestionStepMinutes))
                {
                    suggestions.Add(new TimeRange(start, start + length));
                    today++;
                    if (suggestions.Count == MaxSuggestions)
                        return suggestions;
                }
            }
        }

        return suggestions;
    }

    /// <summary>The parts of a window that no busy time covers, each rounded inwards to 5-minute boundaries.</summary>
    private static List<TimeRange> Subtract(TimeRange window, IEnumerable<TimeRange> busy)
    {
        var free = new List<TimeRange>();
        var cursor = window.StartUtc;

        foreach (var taken in busy.Where(b => b.EndUtc > window.StartUtc && b.StartUtc < window.EndUtc))
        {
            if (taken.StartUtc > cursor)
                Add(cursor, taken.StartUtc);
            if (taken.EndUtc > cursor)
                cursor = taken.EndUtc;
        }

        Add(cursor, window.EndUtc);
        return free;

        void Add(DateTime start, DateTime end)
        {
            var roundedStart = RoundUp(start, WorkingHours.GranularityMinutes);
            var roundedEnd = RoundDown(end, WorkingHours.GranularityMinutes);
            if (roundedEnd > roundedStart)
                free.Add(new TimeRange(roundedStart, roundedEnd));
        }
    }

    // UK time is a whole number of hours from UTC, so a boundary in UTC is one on the UK clock.
    private static DateTime RoundUp(DateTime utc, int minutes)
    {
        var step = TimeSpan.FromMinutes(minutes).Ticks;
        var remainder = utc.Ticks % step;
        return remainder == 0 ? utc : new DateTime(utc.Ticks - remainder + step, DateTimeKind.Utc);
    }

    private static DateTime RoundDown(DateTime utc, int minutes) =>
        new(utc.Ticks - utc.Ticks % TimeSpan.FromMinutes(minutes).Ticks, DateTimeKind.Utc);
}
