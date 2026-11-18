namespace MedApp.Application.Calendar.Common;

/// <summary>
/// The working day: 08:00 to 18:00 UK time, Monday to Friday, except closed days (bank holidays and any other day the
/// clinic is closed: <see cref="ClosedDaySet"/>). Everything on a calendar (appointments, blocks, meetings and template
/// blocks) sits inside it, starting and ending on the same working day, on 5-minute boundaries. Weekends and closed
/// days are shown, greyed, as non-working. Validators ask <see cref="WorkingDays"/>, which looks the closed days up.
/// </summary>
public static class WorkingHours
{
    public static readonly TimeOnly Start = new(8, 0);
    public static readonly TimeOnly End = new(18, 0);

    /// <summary>The calendar grid's rows.</summary>
    public const int SlotMinutes = 15;

    /// <summary>Starts and ends fall on multiples of this.</summary>
    public const int GranularityMinutes = 5;

    /// <summary>The most days one calendar request can cover.</summary>
    public const int MaxDays = 31;

    public const string OutsideWorkingDay = "Times must be within the working day, 08:00 to 18:00 UK time.";
    public const string Weekend = "Calendars are Monday to Friday: weekends aren't working days.";
    public const string EndNotAfterStart = "The end must be after the start.";
    public const string NotOnBoundary = "Times must be on 5-minute boundaries.";
    public const string NotSameDay = "An appointment or block must start and end on the same day.";

    /// <summary>Monday to Friday (closed days aside: see the overload).</summary>
    public static bool IsWorkingDay(DateOnly date) => date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);

    /// <summary>Monday to Friday, and not a closed day.</summary>
    public static bool IsWorkingDay(DateOnly date, ClosedDaySet closedDays) => IsWorkingDay(date) && !closedDays.IsClosed(date);

    /// <summary>Why nothing new can go on a closed day: "The clinic is closed on Friday 25 December (Christmas Day)."</summary>
    public static string ClosedDay(DateOnly date, string name) =>
        $"The clinic is closed on {UkTime.DescribeDate(date)} ({name}): nothing can be booked, blocked or arranged then.";

    /// <summary>Why a UK date can't have anything new on it (a weekend or a closed day), or null if it can.</summary>
    public static string? WhyNotWorkingDay(DateOnly date, ClosedDaySet closedDays) =>
        !IsWorkingDay(date) ? Weekend
        : closedDays.NameOn(date) is { } name ? ClosedDay(date, name)
        : null;

    /// <summary>The working day's start on a UK date, in UTC.</summary>
    public static DateTime DayStartUtc(DateOnly date) => UkTime.ToUtc(date, Start);

    /// <summary>The working day's end on a UK date, in UTC.</summary>
    public static DateTime DayEndUtc(DateOnly date) => UkTime.ToUtc(date, End);

    /// <summary>
    /// Why an appointment, block or meeting can't be at these times, or null if it can. <paramref name="closedDays"/>
    /// must cover the date; pass <see cref="ClosedDaySet.None"/> only where closed days deliberately don't count (a
    /// walk-in, decision 74) or are looked up next (<see cref="WorkingDays"/>). Validators use <see cref="WorkingDays"/>.
    /// </summary>
    public static string? WhyOutside(DateTime startUtc, DateTime endUtc, ClosedDaySet closedDays)
    {
        var start = UkTime.Utc(startUtc);
        var end = UkTime.Utc(endUtc);

        if (end <= start)
            return EndNotAfterStart;

        var date = UkTime.DateOf(start);
        if (UkTime.DateOf(end) != date)
            return NotSameDay;

        if (WhyNotWorkingDay(date, closedDays) is { } notWorking)
            return notWorking;

        if (UkTime.TimeOf(start) < Start || UkTime.TimeOf(end) > End)
            return OutsideWorkingDay;

        // UK time is a whole number of hours from UTC, so a boundary in UTC is one in UK time.
        return IsOnBoundary(start.TimeOfDay) && IsOnBoundary(end.TimeOfDay) ? null : NotOnBoundary;
    }

    /// <summary>Why a day template block can't be at these UK clock times, or null if it can.</summary>
    public static string? WhyOutside(TimeOnly start, TimeOnly end)
    {
        if (end <= start)
            return EndNotAfterStart;

        if (start < Start || end > End)
            return OutsideWorkingDay;

        return IsOnBoundary(start.ToTimeSpan()) && IsOnBoundary(end.ToTimeSpan()) ? null : NotOnBoundary;
    }

    private static bool IsOnBoundary(TimeSpan timeOfDay) =>
        timeOfDay.Ticks % TimeSpan.FromMinutes(GranularityMinutes).Ticks == 0;
}
