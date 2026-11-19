using FluentValidation;
using FluentValidation.Results;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Teams;
using MedApp.Domain.Calendar;

namespace MedApp.Application.Calendar.Common;

/// <summary>
/// The team walls on meetings (MC11 of <c>docs/multi-clinic-plan.md</c>, decision 185; the light demo's
/// <c>canSeeMeeting</c>, <c>findMeeting</c> and <c>ensureMeetingWithOwnTeam</c> in <c>handlers/calendar.ts</c>).
/// <list type="bullet">
/// <item><b>Seeing one:</b> a meeting is the viewer's when they're in it, it's their team's MDT, or anyone in it shares a
/// team with them (<see cref="CalendarWalls.SeesMeeting"/>); opening another is a 404, as if it weren't there,
/// recorded (<c>TeamBoundaryRefused</c>, <see cref="Target"/>).</item>
/// <item><b>The meeting rule:</b> everyone invited shares a team with the organiser (a 400 on
/// <c>AttendeeUserIds</c> naming who doesn't), except an MDT. Admins arrange any meeting.</item>
/// </list>
/// </summary>
public static class MeetingWalls
{
    /// <summary>What a refusal's <c>targetKind</c> says for a meeting.</summary>
    public const string Target = "Meeting";

    /// <summary>The words of a missing meeting's 404, and of one behind the viewer's wall (the same).</summary>
    public static string NotFoundMessage(Guid id) => $"Meeting '{id}' was not found.";

    /// <summary>The meeting rule's 400: who isn't in the organiser's team.</summary>
    public static string NotInYourTeam(IReadOnlyList<string> usernames) =>
        $"{string.Join(", ", usernames)} {(usernames.Count == 1 ? "isn’t" : "aren’t")} in your team. Meetings are with your own team, except an MDT.";

    /// <summary>
    /// Refuses (a 404, recorded) a meeting the viewer can't see. Admins and the system see every one.
    /// </summary>
    public static async Task EnsureVisibleAsync(
        Meeting meeting, TeamScope scope, ICalendarRepository repository, ITeamBoundary boundary, CancellationToken ct)
    {
        if (scope.SeesAllTeams || CalendarWalls.InvolvesOrIsTeams(meeting, scope))
            return;

        var teams = await repository.GetTeamsOfStaffAsync(CalendarWalls.People(meeting).ToList(), ct);
        if (teams.Values.Any(t => t.Overlaps(scope.TeamIds)))
            return;

        throw await boundary.RefuseAsync(Target, meeting.Id, NotFoundMessage(meeting.Id), ct);
    }

    /// <summary>
    /// The meeting rule: everyone invited (the organiser aside) shares a team with the organiser, else a 400 on
    /// <c>AttendeeUserIds</c> naming who doesn't. An MDT (<paramref name="isMdt"/>) may have anyone; admins (and the
    /// system) arrange any meeting.
    /// </summary>
    public static async Task EnsureWithOwnTeamAsync(
        CalendarActor organiser,
        IReadOnlyList<(Guid UserId, string Username)> attendees,
        bool isMdt,
        ICalendarRepository repository,
        CancellationToken ct)
    {
        var scope = organiser.Teams;
        if (isMdt || scope.SeesAllTeams)
            return;

        var invited = attendees.Where(a => a.UserId != organiser.UserId).ToList();
        if (invited.Count == 0)
            return;

        var teams = await repository.GetTeamsOfStaffAsync(invited.Select(a => a.UserId).ToList(), ct);
        var outside = invited
            .Where(a => !(teams.TryGetValue(a.UserId, out var theirs) && theirs.Overlaps(scope.TeamIds)))
            .Select(a => a.Username)
            .ToList();
        if (outside.Count > 0)
            throw new ValidationException([new ValidationFailure(nameof(IMeetingCommand.AttendeeUserIds), NotInYourTeam(outside))]);
    }
}
