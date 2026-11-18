using MedApp.Application.Calendar.Common;

namespace MedApp.DevMcp.DemoData;

/// <summary>
/// When the centre's meetings happen and when an admin arranged them: plain date arithmetic, with tests. The app has no
/// repeating meetings, so an admin arranges each one; the recurring ones are arranged in batches some weeks ahead, as
/// someone setting up the next couple of months would.
/// </summary>
public static class MeetingSchedule
{
    /// <summary>Recurring meetings are arranged eight weeks at a time...</summary>
    public const int BatchWeeks = 8;

    /// <summary>...four weeks before the first of them.</summary>
    public const int BatchLeadWeeks = 4;

    /// <summary>Mandatory training runs every six weeks.</summary>
    public const int TrainingEveryWeeks = 6;

    /// <summary>The training topics, in turn.</summary>
    public static readonly string[] TrainingTitles =
    [
        "Basic life support update",
        "Information governance refresher",
        "Safeguarding update",
        "Infection prevention and control",
        "Fire safety",
        "Moving and handling",
    ];

    /// <summary>Every date from <paramref name="from"/> to <paramref name="to"/> (inclusive) that falls on a weekday.</summary>
    public static IEnumerable<DateOnly> Weekly(DateOnly from, DateOnly to, DayOfWeek day)
    {
        var first = from.AddDays(((int)day - (int)from.DayOfWeek + 7) % 7);
        for (var date = first; date <= to; date = date.AddDays(7))
            yield return date;
    }

    /// <summary>The <paramref name="nth"/> (1 to 4) weekday of each month whose date is from <paramref name="from"/> to <paramref name="to"/>.</summary>
    public static IEnumerable<DateOnly> MonthlyNth(DateOnly from, DateOnly to, DayOfWeek day, int nth)
    {
        if (nth is < 1 or > 4)
            throw new ArgumentOutOfRangeException(nameof(nth), nth, "Every month has at least four of each weekday.");

        for (var month = new DateOnly(from.Year, from.Month, 1); month <= to; month = month.AddMonths(1))
        {
            var date = month.AddDays(((int)day - (int)month.DayOfWeek + 7) % 7 + 7 * (nth - 1));
            if (date >= from && date <= to)
                yield return date;
        }
    }

    /// <summary>
    /// The Monday an admin arranged a recurring meeting's batch: batches cover <see cref="BatchWeeks"/> weeks from
    /// <paramref name="firstMonday"/>, and each is arranged <see cref="BatchLeadWeeks"/> weeks before it starts.
    /// </summary>
    public static DateOnly BatchArrangedOn(DateOnly date, DateOnly firstMonday)
    {
        if (date < firstMonday)
            throw new ArgumentOutOfRangeException(nameof(date), date, "Recurring meetings start on the first Monday.");

        var batch = (date.DayNumber - firstMonday.DayNumber) / 7 / BatchWeeks;
        return firstMonday.AddDays(7 * (batch * BatchWeeks - BatchLeadWeeks));
    }

    /// <summary>The training topic for the <paramref name="index"/>th session.</summary>
    public static string TrainingTitle(int index) => TrainingTitles[index % TrainingTitles.Length];

    /// <summary>A moment between two others: a quarter of the way along, so it's after the first and before the second.</summary>
    public static DateTime Between(DateTime earliest, DateTime latest) =>
        earliest.AddTicks((latest - earliest).Ticks / 4);

    /// <summary>
    /// The first start of <paramref name="minutes"/> on any of <paramref name="dates"/> when everyone in
    /// <paramref name="calendars"/> is free, as the app's suggested times find it (<see cref="Availability"/>), not before
    /// <paramref name="notBefore"/> on the clock nor before <paramref name="nowUtc"/>. Null if there's none.
    /// </summary>
    public static TimeRange? FirstFreeTime(
        IReadOnlyCollection<CalendarData> calendars,
        IReadOnlyCollection<DateOnly> dates,
        int minutes,
        TimeOnly notBefore,
        DateTime nowUtc)
    {
        if (dates.Count == 0)
            return null;

        var free = Availability.FreeTime(calendars, dates.Min(), dates.Max(), nowUtc)
            .Where(day => dates.Contains(day.Date))
            .Select(day =>
            {
                var earliest = UkTime.ToUtc(day.Date, notBefore);
                return day with
                {
                    Free = day.Free
                        .Where(r => r.EndUtc > earliest)
                        .Select(r => r.StartUtc < earliest ? r with { StartUtc = earliest } : r)
                        .ToList(),
                };
            });

        return Availability.Suggestions(free, minutes).FirstOrDefault();
    }
}
