using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Teams;
using MedApp.Domain.Appointments;
using MedApp.Domain.Calendar;

namespace MedApp.Application.Calendar.Common;

/// <summary>
/// The team walls on calendars (MC5 and MC11 of <c>docs/multi-clinic-plan.md</c>; the light demo's
/// <c>canSeeRecord</c>, <c>canSeeMeeting</c> and <c>busyTimeOf</c>): what one viewer sees in full of the appointments and
/// meetings on calendars. Clashes still count every team's rows (nothing here filters what's loaded); only the response
/// is masked.
/// <list type="bullet">
/// <item><b>An appointment</b> is seen when its own team is the viewer's, or its pathway is seen
/// (<see cref="TeamAccess.ByPathway{T}"/> at one record); another team's is busy time only ("Busy"), with no patient,
/// pathway, stage, notes or ID.</item>
/// <item><b>A meeting</b> is seen when the viewer is in it, it's their team's MDT, or anyone in it (its organiser or an
/// attendee) shares a team with them; another is busy time only ("Meeting"), with no title, notes, attendees or ID
/// (Q16).</item>
/// <item>Admins (<see cref="TeamScope.SeesAllTeams"/>) and the system see everything.</item>
/// </list>
/// </summary>
public sealed class CalendarWalls
{
    /// <summary>What another team's appointment is called on a calendar.</summary>
    public const string AppointmentBusy = "Busy";

    /// <summary>What a meeting the viewer can't see is called on a calendar.</summary>
    public const string MeetingBusy = "Meeting";

    /// <summary>What a meeting the viewer can't see is called in a clash or a busy time.</summary>
    public const string HiddenMeeting = "a meeting";

    private readonly IReadOnlySet<Guid> _visiblePathways;
    private readonly IReadOnlyDictionary<Guid, IReadOnlySet<Guid>> _teamsOfStaff;

    private CalendarWalls(
        TeamScope scope, IReadOnlySet<Guid> visiblePathways, IReadOnlyDictionary<Guid, IReadOnlySet<Guid>> teamsOfStaff)
    {
        Scope = scope;
        _visiblePathways = visiblePathways;
        _teamsOfStaff = teamsOfStaff;
    }

    /// <summary>The viewer's team walls.</summary>
    public TeamScope Scope { get; }

    /// <summary>Everything seen: the system's (or an admin's) view, or a viewer whose walls don't apply.</summary>
    public static CalendarWalls None { get; } = new(TeamScope.System, new HashSet<Guid>(), new Dictionary<Guid, IReadOnlySet<Guid>>());

    /// <summary>
    /// The walls for a viewer over these calendars: which of their appointments' pathways the viewer sees
    /// (<paramref name="boundary"/>; null when only meetings matter, as in a clash), and the teams of everyone in their
    /// meetings.
    /// </summary>
    public static async Task<CalendarWalls> ForAsync(
        TeamScope scope,
        IEnumerable<CalendarData> calendars,
        ICalendarRepository repository,
        ITeamBoundary? boundary,
        CancellationToken ct)
    {
        if (scope.SeesAllTeams)
            return new CalendarWalls(scope, new HashSet<Guid>(), new Dictionary<Guid, IReadOnlySet<Guid>>());

        var list = calendars.ToList();
        var pathwayIds = list.SelectMany(c => c.Appointments)
            .Where(a => !scope.IsMember(a.TeamId) && a.PathwayId is not null)
            .Select(a => a.PathwayId!.Value)
            .Distinct()
            .ToList();
        var visible = boundary is null || pathwayIds.Count == 0
            ? new HashSet<Guid>()
            : await boundary.VisiblePathwaysAsync(pathwayIds, scope, ct);

        var people = list.SelectMany(c => c.Meetings)
            .Where(m => !InvolvesOrIsTeams(m, scope))
            .SelectMany(People)
            .Distinct()
            .ToList();
        var teams = people.Count == 0
            ? new Dictionary<Guid, IReadOnlySet<Guid>>()
            : await repository.GetTeamsOfStaffAsync(people, ct);

        return new CalendarWalls(scope, visible, teams);
    }

    /// <summary>Whether the viewer sees this appointment in full.</summary>
    public bool SeesAppointment(Appointment appointment) =>
        Scope.SeesAllTeams
        || Scope.IsMember(appointment.TeamId)
        || (appointment.PathwayId is { } pathwayId && _visiblePathways.Contains(pathwayId));

    /// <summary>Whether the viewer sees this meeting in full ("yours", MC11).</summary>
    public bool SeesMeeting(Meeting meeting) =>
        Scope.SeesAllTeams
        || InvolvesOrIsTeams(meeting, Scope)
        || People(meeting).Any(id => _teamsOfStaff.TryGetValue(id, out var teams) && teams.Overlaps(Scope.TeamIds));

    /// <summary>Whether a meeting is the viewer's: they're in it, or it's one of their teams' MDTs.</summary>
    public static bool InvolvesOrIsTeams(Meeting meeting, TeamScope scope) =>
        People(meeting).Contains(scope.UserId) || (meeting.TeamId is { } teamId && scope.IsMember(teamId));

    /// <summary>Everyone in a meeting: its organiser and every attendee (whatever their reply).</summary>
    public static IEnumerable<Guid> People(Meeting meeting) =>
        meeting.Attendees.Select(a => a.StaffUserId).Prepend(meeting.OrganiserUserId).Distinct();
}
