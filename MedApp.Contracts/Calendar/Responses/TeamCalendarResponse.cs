namespace MedApp.Contracts.Calendar.Responses;

/// <summary>A team's calendar for one UK date (MC11): its members side by side, and when they're all free.</summary>
public sealed class TeamCalendarResponse
{
    public DateOnly Date { get; set; }

    /// <summary>The team shown: the one asked for, else the viewer's first (by name), else the first team; none if there are none.</summary>
    public Guid? TeamId { get; set; }

    public string? TeamName { get; set; }

    public CalendarDayResponse Day { get; set; } = new();

    public List<CalendarResponse> Members { get; set; } = [];

    public List<TimeRangeResponse> EveryoneFree { get; set; } = [];

    /// <summary>Everyone in the team (for choosing whose calendars to show), by username.</summary>
    public List<StaffMemberResponse> TeamMembers { get; set; } = [];
}
