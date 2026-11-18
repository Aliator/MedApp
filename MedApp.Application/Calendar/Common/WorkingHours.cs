using System.Globalization;

namespace MedApp.Application.Calendar.Common;

/// <summary>
/// The fixed rules of a working day, and the default hours. Whether someone works on a date, and when, is
/// <see cref="WorkingTime"/>: their own hours (<see cref="Domain.Calendar.StaffHours"/>), inside their site's
/// (<see cref="Domain.Sites.SiteHours"/>), less closed days; a person (or site) with no hours of their own works the
/// default, Monday to Friday, 08:00 to 18:00 UK time (centre plan decision 119). Everything on a calendar (appointments,
/// blocks, meetings and template blocks) sits inside one person's working day, on 5-minute boundaries, between 06:00
/// and 23:00 UK time, so a working day never spans a clock change. Validators ask <see cref="WorkingDays"/>.
/// </summary>
public static class WorkingHours
{
    /// <summary>The default day's start, for anyone, and any site, with no hours of their own.</summary>
    public static readonly TimeOnly Start = new(8, 0);

    /// <summary>The default day's end.</summary>
    public static readonly TimeOnly End = new(18, 0);

    /// <summary>The earliest any hours can start (decision 119's bounds).</summary>
    public static readonly TimeOnly Earliest = new(6, 0);

    /// <summary>The latest any hours can end.</summary>
    public static readonly TimeOnly Latest = new(23, 0);

    /// <summary>The calendar grid's rows.</summary>
    public const int SlotMinutes = 15;

    /// <summary>Starts and ends fall on multiples of this.</summary>
    public const int GranularityMinutes = 5;

    /// <summary>The most days one calendar request can cover.</summary>
    public const int MaxDays = 31;

    /// <summary>Who a message is about when it isn't named.</summary>
    public const string ThisPerson = "This member of staff";

    /// <summary>Outside the default day (<see cref="Within(TimeOnly, TimeOnly)"/> for 08:00 to 18:00).</summary>
    public const string OutsideWorkingDay = "Times must be within the working day, 08:00 to 18:00 UK time.";

    public const string OutsideBounds = "Times must be between 06:00 and 23:00 UK time.";
    public const string EndNotAfterStart = "The end must be after the start.";
    public const string NotOnBoundary = "Times must be on 5-minute boundaries.";
    public const string NotSameDay = "An appointment or block must start and end on the same day.";

    /// <summary>Monday to Friday: the default's days (closed days aside: see the overload).</summary>
    public static bool IsWorkingDay(DateOnly date) => IsDefaultDay(date.DayOfWeek);

    /// <summary>The default's days, Monday to Friday.</summary>
    public static bool IsDefaultDay(DayOfWeek day) => day is not (DayOfWeek.Saturday or DayOfWeek.Sunday);

    /// <summary>A default day (Monday to Friday), and not closed everywhere.</summary>
    public static bool IsWorkingDay(DateOnly date, ClosedDaySet closedDays) => IsWorkingDay(date) && !closedDays.IsClosed(date);

    /// <summary>Why nothing new can go on a closed day: "The clinic is closed on Friday 25 December (Christmas Day)."</summary>
    public static string ClosedDay(DateOnly date, string name) =>
        $"The clinic is closed on {UkTime.DescribeDate(date)} ({name}): nothing can be booked, blocked or arranged then.";

    /// <summary>Why someone has nothing new on a day they don't work: "k.ashford doesn't work on Saturdays."</summary>
    public static string NotWorking(DayOfWeek day, string? who = null) => $"{who ?? ThisPerson} doesn't work on {day}s.";

    /// <summary>Why a time is outside one person's day: "Times must be within the working day, 09:00 to 13:00 UK time."</summary>
    public static string Within(TimeOnly start, TimeOnly end) =>
        $"Times must be within the working day, {Describe(start)} to {Describe(end)} UK time.";

    /// <summary>Why a time is outside a named person's day, for a meeting: "k.ashford works 09:00 to 13:00 UK time that day: …"</summary>
    public static string Within(TimeOnly start, TimeOnly end, string who) =>
        $"{who} works {Describe(start)} to {Describe(end)} UK time that day: the time must be inside everyone's working day.";

    /// <summary>The default day's start on a UK date, in UTC.</summary>
    public static DateTime DayStartUtc(DateOnly date) => UkTime.ToUtc(date, Start);

    /// <summary>The default day's end on a UK date, in UTC.</summary>
    public static DateTime DayEndUtc(DateOnly date) => UkTime.ToUtc(date, End);

    /// <summary>"08:00".</summary>
    public static string Describe(TimeOnly time) => time.ToString("HH:mm", CultureInfo.InvariantCulture);

    /// <summary>
    /// Why something can't be at these times whoever it's for, or null if it could be: it ends after it starts, on the
    /// same UK day, on 5-minute boundaries, between 06:00 and 23:00, and not on a day closed everywhere. Whether the
    /// person works then is <see cref="WorkingTime.WhyOutside"/>. Pass <see cref="ClosedDaySet.None"/> only where closed
    /// days deliberately don't count (a walk-in, decision 74) or are looked up next (<see cref="WorkingDays"/>).
    /// </summary>
    public static string? WhyOutside(DateTime startUtc, DateTime endUtc, ClosedDaySet closedDays)
    {
        if (WhyBadShape(startUtc, endUtc) is { } why)
            return why;

        var date = UkTime.DateOf(UkTime.Utc(startUtc));
        return closedDays.NameOn(date) is { } name ? ClosedDay(date, name) : null;
    }

    /// <summary>Why these times can't be anyone's, closed days aside (see <see cref="WhyOutside(DateTime, DateTime, ClosedDaySet)"/>), or null.</summary>
    public static string? WhyBadShape(DateTime startUtc, DateTime endUtc)
    {
        var start = UkTime.Utc(startUtc);
        var end = UkTime.Utc(endUtc);

        if (end <= start)
            return EndNotAfterStart;

        if (UkTime.DateOf(end) != UkTime.DateOf(start))
            return NotSameDay;

        // UK time is a whole number of hours from UTC, so a boundary in UTC is one in UK time.
        if (!IsOnBoundary(start.TimeOfDay) || !IsOnBoundary(end.TimeOfDay))
            return NotOnBoundary;

        return UkTime.TimeOf(start) < Earliest || UkTime.TimeOf(end) > Latest ? OutsideBounds : null;
    }

    /// <summary>
    /// Why a day template block, or someone's or a site's hours, can't be at these UK clock times, or null if they can:
    /// between 06:00 and 23:00, on 5-minute boundaries, the end after the start.
    /// </summary>
    public static string? WhyOutside(TimeOnly start, TimeOnly end)
    {
        if (end <= start)
            return EndNotAfterStart;

        if (start < Earliest || end > Latest)
            return OutsideBounds;

        return IsOnBoundary(start.ToTimeSpan()) && IsOnBoundary(end.ToTimeSpan()) ? null : NotOnBoundary;
    }

    private static bool IsOnBoundary(TimeSpan timeOfDay) =>
        timeOfDay.Ticks % TimeSpan.FromMinutes(GranularityMinutes).Ticks == 0;
}
