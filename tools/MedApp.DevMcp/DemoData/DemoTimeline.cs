using MedApp.Application.Calendar.Common;

namespace MedApp.DevMcp.DemoData;

/// <summary>
/// The moments in the demo's story, all relative to "now" as the light demo's are (<c>MedApp.Web/src/demo/time.ts</c>):
/// a UK time on a day so many days ago, the open days either side of today, and this week's days. Visits keep to
/// weekdays (and off the closed days the seed adds), and what happens "today" is kept in the past.
/// </summary>
public sealed class DemoTimeline
{
    public DemoTimeline(DateTime nowUtc)
    {
        NowUtc = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);
        Today = UkTime.DateOf(NowUtc);
        ThisMonday = Today.AddDays(-(((int)Today.DayOfWeek + 6) % 7));
        ClosedDays = [NextDate(12, 25), NextDate(12, 26), NextDate(1, 1)];
    }

    public DateTime NowUtc { get; }

    /// <summary>Today, UK time.</summary>
    public DateOnly Today { get; }

    /// <summary>Whether it's seeded on a Saturday or Sunday, when the centre and the four demo accounts work the weekend.</summary>
    public bool IsWeekend => Today.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    /// <summary>This week's Monday (UK).</summary>
    public DateOnly ThisMonday { get; }

    /// <summary>The bank holidays the centre closes on (the next of each), as the light demo's setup has them.</summary>
    public IReadOnlyList<DateOnly> ClosedDays { get; }

    /// <summary>The next <paramref name="month"/>-<paramref name="day"/> that's today or later.</summary>
    public DateOnly NextDate(int month, int day)
    {
        var thisYear = new DateOnly(Today.Year, month, day);
        return thisYear >= Today ? thisYear : thisYear.AddYears(1);
    }

    /// <summary>A UK clock time on a UK date, in UTC.</summary>
    public static DateTime At(DateOnly date, int hour, int minute = 0) => UkTime.ToUtc(date, new TimeOnly(hour, minute));

    /// <summary>Today plus <paramref name="days"/> (negative: before).</summary>
    public DateOnly DaysFrom(int days) => Today.AddDays(days);

    /// <summary>A moment <paramref name="days"/> before today at a UK time of day (the light demo's <c>ago</c>).</summary>
    public DateTime Ago(int days, int hour = 10, int minute = 0) => At(Today.AddDays(-days), hour, minute);

    /// <summary>Whether the centre is open on a date: a weekday, and not one of its closed days.</summary>
    public bool IsOpen(DateOnly date) => WorkingHours.IsWorkingDay(date) && !ClosedDays.Contains(date);

    /// <summary>The <paramref name="n"/>-th open day after today (1 is the next one), so a booking is always ahead.</summary>
    public DateOnly OpenDayAhead(int n)
    {
        var date = Today;
        for (var i = 0; i < n; i++)
        {
            date = date.AddDays(1);
            while (!IsOpen(date))
                date = date.AddDays(1);
        }

        return date;
    }

    /// <summary>The first <paramref name="count"/> open days from today (today, if it's one).</summary>
    public IReadOnlyList<DateOnly> OpenDaysFromToday(int count)
    {
        var days = new List<DateOnly>();
        for (var date = Today; days.Count < count; date = date.AddDays(1))
            if (IsOpen(date))
                days.Add(date);
        return days;
    }

    /// <summary>About <paramref name="n"/> days ago, moved back to a weekday (clinics don't run at weekends).</summary>
    public DateOnly WeekdayBack(int n)
    {
        var date = Today.AddDays(-n);
        while (!WorkingHours.IsWorkingDay(date))
            date = date.AddDays(-1);
        return date;
    }

    /// <summary>The latest day on or before <paramref name="date"/> that's one of <paramref name="days"/>.</summary>
    public static DateOnly OnOrBefore(DateOnly date, params DayOfWeek[] days)
    {
        while (!days.Contains(date.DayOfWeek))
            date = date.AddDays(-1);
        return date;
    }

    /// <summary>
    /// The latest weekday whose <paramref name="hour"/>:<paramref name="minute"/> plus <paramref name="minutes"/> is in the
    /// past (today's, once it's over; else the last weekday's): a visit "this morning" that has happened.
    /// </summary>
    public DateTime LastPast(int hour, int minute, int minutes)
    {
        var date = Today;
        while (!WorkingHours.IsWorkingDay(date) || At(date, hour, minute).AddMinutes(minutes) > NowUtc.AddMinutes(-10))
            date = date.AddDays(-1);
        return At(date, hour, minute);
    }

    /// <summary>This week's day <paramref name="offset"/> (0 is Monday; 7 is next Monday).</summary>
    public DateOnly ThisWeek(int offset) => ThisMonday.AddDays(offset);

    /// <summary>The moment, or a few minutes before now if it's later: what happened today stays in the past.</summary>
    public DateTime Past(DateTime utc) => utc < NowUtc.AddMinutes(-2) ? utc : NowUtc.AddMinutes(-2);

    /// <summary><paramref name="hours"/> before now.</summary>
    public DateTime HoursAgo(double hours) => NowUtc.AddHours(-hours);
}
