namespace MedApp.Application.Calendar.Common;

/// <summary>
/// The working day: 08:00 to 18:00 UK time, Monday to Friday. Everything on a calendar (appointments, blocks and
/// template blocks) sits inside it, starting and ending on the same weekday, on 5-minute boundaries. Weekends are
/// shown, greyed, as non-working. Bank holidays aren't known yet.
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

    public static bool IsWorkingDay(DateOnly date) => date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);

    /// <summary>The working day's start on a UK date, in UTC.</summary>
    public static DateTime DayStartUtc(DateOnly date) => UkTime.ToUtc(date, Start);

    /// <summary>The working day's end on a UK date, in UTC.</summary>
    public static DateTime DayEndUtc(DateOnly date) => UkTime.ToUtc(date, End);

    /// <summary>Why an appointment or block can't be at these times, or null if it can.</summary>
    public static string? WhyOutside(DateTime startUtc, DateTime endUtc)
    {
        var start = UkTime.Utc(startUtc);
        var end = UkTime.Utc(endUtc);

        if (end <= start)
            return EndNotAfterStart;

        var date = UkTime.DateOf(start);
        if (UkTime.DateOf(end) != date)
            return NotSameDay;

        if (!IsWorkingDay(date))
            return Weekend;

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
