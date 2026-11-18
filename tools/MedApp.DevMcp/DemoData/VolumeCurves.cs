using MedApp.Application.Calendar.Common;
using MedApp.Application.Pathways.Insights.Common;

namespace MedApp.DevMcp.DemoData;

/// <summary>The demo centre's pathways, one per pathway template.</summary>
public enum PathwayKind
{
    /// <summary>The skin lesion pathway as redesigned <see cref="VolumeCurves.SkinLesionRedesignedWeeksAgo"/> weeks ago.</summary>
    SkinLesion,

    /// <summary>The older skin lesion pathway it replaced: archived, with a few still finishing.</summary>
    SkinLesion2024,
    InflammatorySkin,
    PostMenopausalBleeding,
    Prostate,
    Diabetes,
    Biomechanics,
}

/// <summary>
/// How many referrals each pathway takes a week, over the centre's 18 months: the shape Insights shows. Weeks are
/// counted back from this one (0), as UK weeks, Monday to Sunday, as Insights counts them. The last 26 weeks are the
/// detailed history; before that it's thinner (<see cref="ThinHistory"/>), as if fewer were recorded in the app's
/// first months. Two pathways follow the seasons (<see cref="Season"/>), and one the calendar month, so the shape
/// depends on the time of year the seed is run, as a centre's does.
/// </summary>
public static class VolumeCurves
{
    /// <summary>How far back the centre's use of the app goes: about 18 months.</summary>
    public const int HistoryWeeks = 78;

    /// <summary>The detailed weeks: what Insights' longest period shows.</summary>
    public const int DetailedWeeks = 26;

    /// <summary>The share of the usual volume recorded before the detailed weeks.</summary>
    public const double ThinHistory = 0.2;

    /// <summary>The redesigned skin lesion pathway's first week (weeks ago); it replaced the 2024 one, archived then.</summary>
    public const int SkinLesionRedesignedWeeksAgo = 18;

    /// <summary>How many weeks the redesigned pathway took to reach its full volume.</summary>
    public const int SkinLesionRampWeeks = 6;

    /// <summary>The biomechanics service's first week (weeks ago).</summary>
    public const int BiomechanicsStartedWeeksAgo = 20;

    /// <summary>
    /// The expected number of referrals a pathway takes in a whole week, <paramref name="weeksAgo"/> weeks back, the
    /// week starting <paramref name="monday"/>.
    /// <list type="bullet">
    /// <item>Skin lesion: launched <see cref="SkinLesionRedesignedWeeksAgo"/> weeks ago, ramping up from 3 to 8 a week
    /// over <see cref="SkinLesionRampWeeks"/> weeks; more in summer, fewer in winter.</item>
    /// <item>Skin lesion (2024): about 5 a week (with the same seasons) until the redesign replaced it; nothing since.</item>
    /// <item>Inflammatory skin review: 3.5 a week, more in winter; but 1 last week, when a clinic was cancelled.</item>
    /// <item>Post-menopausal bleeding: 2 a week; 1 every other week while the visiting consultant was away (8 to 11
    /// weeks ago), and 4 in the two weeks of extra clinics (2 and 3 weeks ago).</item>
    /// <item>Prostate assessment: a steady 2 a week.</item>
    /// <item>Diabetes review: 2 a week, and 6 in the week the month's recall letters go out (the week with the 1st).</item>
    /// <item>Biomechanics course: 2 a week since it started <see cref="BiomechanicsStartedWeeksAgo"/> weeks ago.</item>
    /// </list>
    /// </summary>
    public static double Expected(PathwayKind pathwayKind, int weeksAgo, DateOnly monday)
    {
        if (weeksAgo < 0 || weeksAgo >= HistoryWeeks)
            return 0;

        var thin = weeksAgo >= DetailedWeeks ? ThinHistory : 1.0;
        return pathwayKind switch
        {
            PathwayKind.SkinLesion => weeksAgo > SkinLesionRedesignedWeeksAgo
                ? 0
                : Math.Min(8, 3 + (SkinLesionRedesignedWeeksAgo - weeksAgo) * 5.0 / SkinLesionRampWeeks) * Season(pathwayKind, monday),
            PathwayKind.SkinLesion2024 => weeksAgo <= SkinLesionRedesignedWeeksAgo ? 0 : 5 * thin * Season(pathwayKind, monday),
            PathwayKind.InflammatorySkin => weeksAgo == 1 ? 1 : 3.5 * thin * Season(pathwayKind, monday),
            PathwayKind.PostMenopausalBleeding => weeksAgo switch
            {
                2 or 3 => 4,
                >= 8 and <= 11 => 0.5,
                _ => 2 * thin,
            },
            PathwayKind.Prostate => 2 * thin,
            PathwayKind.Diabetes => (IsRecallWeek(monday) ? 6 : 2) * thin,
            PathwayKind.Biomechanics => weeksAgo <= BiomechanicsStartedWeeksAgo ? 2 : 0,
            _ => 0,
        };
    }

    /// <summary>The same, for the week <paramref name="weeksAgo"/> weeks before the one <paramref name="nowUtc"/> is in.</summary>
    public static double Expected(PathwayKind pathwayKind, int weeksAgo, DateTime nowUtc) =>
        Expected(pathwayKind, weeksAgo, InsightsPeriod.MondayOf(UkTime.DateOf(nowUtc)).AddDays(-7 * weeksAgo));

    /// <summary>
    /// How the time of year changes a pathway's referrals, by the month its week starts in: people notice moles in
    /// summer clothes (May to September), and eczema and psoriasis flare in the cold (November to March). 1 for the
    /// pathways that don't follow the seasons.
    /// </summary>
    public static double Season(PathwayKind pathwayKind, DateOnly monday) => pathwayKind switch
    {
        PathwayKind.SkinLesion or PathwayKind.SkinLesion2024 => monday.Month switch
        {
            >= 5 and <= 9 => 1.25,
            12 or 1 or 2 => 0.8,
            _ => 1,
        },
        PathwayKind.InflammatorySkin => monday.Month switch
        {
            >= 11 or <= 3 => 1.25,
            >= 6 and <= 8 => 0.8,
            _ => 1,
        },
        _ => 1,
    };

    /// <summary>Whether a week (Monday to Sunday) has the 1st of a month in it: when the diabetes recall letters go out.</summary>
    public static bool IsRecallWeek(DateOnly monday) => monday.Day == 1 || monday.AddDays(6).Month != monday.Month;

    /// <summary>
    /// When a pathway's referrals were recorded, oldest first: each week's expected number (this week's in proportion
    /// to how much of it has gone), rounded up or down at random, each at a random moment of the working day on a
    /// weekday of that week, and never after <paramref name="nowUtc"/>.
    /// </summary>
    public static IReadOnlyList<DateTime> CreationTimes(PathwayKind pathwayKind, DateTime nowUtc, Random random)
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

            var expected = Expected(pathwayKind, weeksAgo, monday) * share;
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
