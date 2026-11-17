namespace MedApp.Contracts.Calendar.Responses;

/// <summary>
/// One UK date of several members of staff's calendars, side by side, and the times they're all free then (after now,
/// inside the working day).
/// </summary>
public sealed class TeamCalendarResponse
{
    public DateOnly Date { get; set; }

    /// <summary>The date itself: whether it's worked, past or today, and its working day in UTC.</summary>
    public CalendarDayResponse Day { get; set; } = new();

    /// <summary>Each person's calendar for the date, by username.</summary>
    public List<CalendarResponse> Members { get; set; } = [];

    /// <summary>When everyone shown is free, by start time.</summary>
    public List<TimeRangeResponse> EveryoneFree { get; set; } = [];
}
