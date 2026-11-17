namespace MedApp.Contracts.Calendar.Requests;

/// <summary>Query string for <c>GET api/calendar/team</c>: one UK date of several people's calendars.</summary>
public sealed class GetTeamCalendarRequest
{
    public DateOnly Date { get; set; }

    /// <summary>Whose calendars; everyone with a staff role when left out.</summary>
    public List<Guid>? StaffUserIds { get; set; }
}
