namespace MedApp.Contracts.Calendar.Requests;

/// <summary>Query string for <c>GET api/calendar/team</c>: one UK date of a team's calendars.</summary>
public sealed class GetTeamCalendarRequest
{
    public DateOnly Date { get; set; }

    /// <summary>Which team (any): the viewer's first (by name) when left out; an admin's, the first team by name.</summary>
    public Guid? TeamId { get; set; }

    /// <summary>Whose calendars, of the team's members (others are left out); everyone in the team when left out.</summary>
    public List<Guid>? StaffUserIds { get; set; }
}
