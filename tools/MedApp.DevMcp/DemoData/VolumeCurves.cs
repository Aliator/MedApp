using MedApp.Application.Calendar.Common;
using MedApp.Application.Procedures.Insights.Common;

namespace MedApp.DevMcp.DemoData;

/// <summary>The demo clinic's pathways, one per procedure template.</summary>
public enum Pathway
{
    DayCase,
    DayCase2024,
    Surgical,
    Diagnostic,
    Minor,
    Physio,
}

/// <summary>
/// How many procedures each pathway starts a week, over the clinic's 18 months: the shape Insights shows. Weeks are
/// counted back from this one (0), as UK weeks, Monday to Sunday, as Insights counts them. The last 26 weeks are the
/// detailed history; before that it's thinner (<see cref="ThinHistory"/>), as if fewer were recorded in the app's
/// first months.
/// </summary>
public static class VolumeCurves
{
    /// <summary>How far back the clinic's use of the app goes: about 18 months.</summary>
    public const int HistoryWeeks = 78;

    /// <summary>The detailed weeks: what Insights' longest period shows.</summary>
    public const int DetailedWeeks = 26;

    /// <summary>The share of the usual volume recorded before the detailed weeks.</summary>
    public const double ThinHistory = 0.2;

    /// <summary>The new day-case pathway's first week (weeks ago); it replaced the 2024 one, archived then.</summary>
    public const int DayCaseLaunchedWeeksAgo = 9;

    /// <summary>The physiotherapy service's first week (weeks ago).</summary>
    public const int PhysioStartedWeeksAgo = 15;

    /// <summary>
    /// The expected number of procedures a pathway starts in a whole week, <paramref name="weeksAgo"/> weeks back.
    /// <list type="bullet">
    /// <item>Day-case: launched <see cref="DayCaseLaunchedWeeksAgo"/> weeks ago, ramping up from 3 to 9 a week.</item>
    /// <item>Day-case (2024): about 5 a week until the new one replaced it; nothing since.</item>
    /// <item>Surgical: a steady 2.5 a week.</item>
    /// <item>Diagnostic: about 6 a week, 3 in the summer (8 to 11 weeks ago), and 12 in the waiting-list initiative
    /// (2 and 3 weeks ago).</item>
    /// <item>Minor procedures: 3.5 a week, but 1 last week, when a clinic was cancelled.</item>
    /// <item>Physiotherapy: 2 a week since it started <see cref="PhysioStartedWeeksAgo"/> weeks ago.</item>
    /// </list>
    /// </summary>
    public static double Expected(Pathway pathway, int weeksAgo)
    {
        if (weeksAgo < 0 || weeksAgo >= HistoryWeeks)
            return 0;

        var thin = weeksAgo >= DetailedWeeks ? ThinHistory : 1.0;
        return pathway switch
        {
            Pathway.DayCase => weeksAgo > DayCaseLaunchedWeeksAgo
                ? 0
                : 3 + (DayCaseLaunchedWeeksAgo - weeksAgo) * 6.0 / DayCaseLaunchedWeeksAgo,
            Pathway.DayCase2024 => weeksAgo <= DayCaseLaunchedWeeksAgo ? 0 : 5 * thin,
            Pathway.Surgical => 2.5 * thin,
            Pathway.Diagnostic => weeksAgo switch
            {
                2 or 3 => 12,
                >= 8 and <= 11 => 3,
                _ => 6 * thin,
            },
            Pathway.Minor => weeksAgo == 1 ? 1 : 3.5 * thin,
            Pathway.Physio => weeksAgo <= PhysioStartedWeeksAgo ? 2 : 0,
            _ => 0,
        };
    }

    /// <summary>
    /// When a pathway's procedures were created, oldest first: each week's expected number (this week's in proportion
    /// to how much of it has gone), rounded up or down at random, each at a random moment of the working day on a
    /// weekday of that week, and never after <paramref name="nowUtc"/>.
    /// </summary>
    public static IReadOnlyList<DateTime> CreationTimes(Pathway pathway, DateTime nowUtc, Random random)
    {
        var thisMonday = InsightsPeriod.MondayOf(UkTime.DateOf(nowUtc));
        var times = new List<DateTime>();

        for (var weeksAgo = HistoryWeeks - 1; weeksAgo >= 0; weeksAgo--)
        {
            var monday = thisMonday.AddDays(-7 * weeksAgo);
            var weekStart = UkTime.ToUtc(monday, TimeOnly.MinValue);
            var share = weeksAgo == 0
                ? Math.Clamp((nowUtc - weekStart).TotalDays / 7, 0, 1)
                : 1;

            var expected = Expected(pathway, weeksAgo) * share;
            var count = (int)Math.Floor(expected) + (random.NextDouble() < expected - Math.Floor(expected) ? 1 : 0);

            var week = new List<DateTime>();
            for (var i = 0; i < count; i++)
            {
                var time = WorkingMoment(monday, weeksAgo == 0 ? nowUtc : null, random);
                if (time is { } t)
                    week.Add(t);
            }

            times.AddRange(week.Order());
        }

        return times;
    }

    /// <summary>A random moment in the working day of a weekday of the week starting <paramref name="monday"/>, before <paramref name="notAfterUtc"/> if given.</summary>
    private static DateTime? WorkingMoment(DateOnly monday, DateTime? notAfterUtc, Random random)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var date = monday.AddDays(random.Next(5));
            var minutes = random.Next(8 * 60 + 15, 17 * 60 + 30);
            var time = UkTime.ToUtc(date, new TimeOnly(minutes / 60, minutes % 60));
            if (notAfterUtc is null || time < notAfterUtc)
                return time;
        }

        return null;
    }
}
