using MedApp.Application.Procedures.Common;

namespace MedApp.Application.Procedures.Insights.Common;

/// <summary>One UK week: Monday 00:00 to the next Monday 00:00, UK time, as moments in UTC.</summary>
public sealed record InsightsWeek(DateOnly Monday, DateTime StartUtc, DateTime EndUtc)
{
    public bool Contains(DateTime utc) => utc >= StartUtc && utc < EndUtc;
}

/// <summary>
/// The weeks insights cover, ending with the current one (so far), and the moments its comparisons use. Weeks are UK
/// weeks, starting on Monday, as due dates are UK days (<see cref="DueDates"/>).
/// <para>
/// A comparison with last week is made at the same point in it: as long after last week's Monday 00:00 as now is after
/// this week's. So on a Tuesday morning, "completed this week" is set against last Monday and Tuesday morning, not the
/// whole of last week.
/// </para>
/// </summary>
public sealed class InsightsPeriod
{
    private InsightsPeriod(DateTime nowUtc, IReadOnlyList<InsightsWeek> weeks)
    {
        NowUtc = nowUtc;
        Weeks = weeks;
        var lastWeekStart = Week(ThisWeek.Monday.AddDays(-7)).StartUtc;
        LastWeekStartUtc = lastWeekStart;
        SameMomentLastWeekUtc = lastWeekStart + (nowUtc - ThisWeek.StartUtc);
    }

    public DateTime NowUtc { get; }

    /// <summary>The weeks, oldest first; the last is this week, which hasn't finished.</summary>
    public IReadOnlyList<InsightsWeek> Weeks { get; }

    public InsightsWeek ThisWeek => Weeks[^1];

    /// <summary>The start of the first week: what "the last N weeks" counts from.</summary>
    public DateTime StartUtc => Weeks[0].StartUtc;

    /// <summary>Monday 00:00 last week, UK time.</summary>
    public DateTime LastWeekStartUtc { get; }

    /// <summary>The moment last week that is as far into it as now is into this week.</summary>
    public DateTime SameMomentLastWeekUtc { get; }

    /// <summary>The <paramref name="weeks"/> UK weeks up to and including the one <paramref name="nowUtc"/> is in.</summary>
    public static InsightsPeriod For(DateTime nowUtc, int weeks)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(weeks, 1);
        var thisMonday = MondayOf(DueDates.ToUkDate(nowUtc));
        var list = Enumerable.Range(0, weeks)
            .Select(i => Week(thisMonday.AddDays(-7 * (weeks - 1 - i))))
            .ToList();
        return new InsightsPeriod(nowUtc, list);
    }

    /// <summary>The Monday of the week a day is in.</summary>
    public static DateOnly MondayOf(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    private static InsightsWeek Week(DateOnly monday) =>
        new(monday, DueDates.StartOfDayUtc(monday), DueDates.StartOfDayUtc(monday.AddDays(7)));
}
