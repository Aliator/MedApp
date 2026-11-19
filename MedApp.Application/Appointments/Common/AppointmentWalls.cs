using FluentValidation;
using FluentValidation.Results;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Teams;
using MedApp.Application.Teams.Repositories;
using MedApp.Domain.Appointments;

namespace MedApp.Application.Appointments.Common;

/// <summary>
/// The team walls on appointments (MC5 of <c>docs/multi-clinic-plan.md</c>; the light demo's <c>findAppointment</c>,
/// <c>appointmentToChange</c>, <c>ensureStaffInTeam</c> and <c>teamForBooking</c> in <c>handlers/calendar.ts</c>).
/// <list type="bullet">
/// <item><b>Seen</b> when its own team is the viewer's or its pathway is seen (<see cref="TeamAccess.ByPathway{T}"/>);
/// another's by ID is a 404, as if it weren't there, recorded (<c>TeamBoundaryRefused</c>, <see cref="Target"/>).</item>
/// <item><b>Changed only by its own team</b>: anyone else who sees it (an admin, the lab) gets a 409
/// (<see cref="TeamWalls.RecordNotYours"/>, "This appointment").</item>
/// <item><b>The booking rule:</b> its clinician belongs to its team (a 400 on <c>StaffUserId</c>), so they can see its
/// patient.</item>
/// <item><b>A new one's team</b> is its pathway's; without one, the booker's (<see cref="TeamScope.DefaultTeamId"/>), else
/// the clinician's (<see cref="TeamFor"/>).</item>
/// </list>
/// </summary>
public static class AppointmentWalls
{
    /// <summary>What a refusal's <c>targetKind</c> says for an appointment.</summary>
    public const string Target = "Appointment";

    /// <summary>What the 409 for another team's appointment calls it.</summary>
    public const string What = "This appointment";

    /// <summary>The words of a missing appointment's 404, and of one behind the viewer's wall (the same).</summary>
    public static string NotFoundMessage(Guid id) => $"Appointment '{id}' was not found.";

    /// <summary>The booking rule's 400: the clinician isn't in the appointment's team.</summary>
    public static string StaffNotInTeam(string? staffUsername, string? teamName) =>
        $"{staffUsername ?? "They"} isn’t in {teamName ?? "the appointment’s team"}, so can’t see its patients. Choose someone in the team.";

    /// <summary>Why the viewer sees the appointment (owner, stage owner, consult, MDT, admin), or a 404, recorded.</summary>
    public static Task<TeamAccessKind> EnsureVisibleAsync(
        this ITeamBoundary boundary, Appointment appointment, TeamScope scope, CancellationToken ct) =>
        boundary.EnsureRecordVisibleAsync(
            Target, appointment.Id, appointment.TeamId, appointment.PathwayId, scope, NotFoundMessage(appointment.Id), ct);

    /// <summary>For a change: the appointment seen (else a 404, recorded) and its team the viewer's (else a 409).</summary>
    public static async Task EnsureCanChangeAsync(
        this ITeamBoundary boundary, Appointment appointment, TeamScope scope, CancellationToken ct)
    {
        await boundary.EnsureVisibleAsync(appointment, scope, ct);
        await boundary.EnsureCanChangeTeamAsync(appointment.TeamId, scope, What, ct);
    }

    /// <summary>
    /// A new appointment's team, when one decides it: its pathway's (<paramref name="pathwayTeamId"/>), else one of the
    /// booker's (<paramref name="booker"/>), else one of the clinician's. Null when none does (the caller falls back to
    /// General, and the booking rule doesn't apply).
    /// </summary>
    public static async Task<Guid?> TeamForAsync(
        Guid? pathwayTeamId, TeamScope booker, Guid? staffUserId, ICalendarRepository calendars, CancellationToken ct)
    {
        if ((pathwayTeamId ?? booker.DefaultTeamId) is { } decided)
            return decided;
        if (staffUserId is not { } staff)
            return null;

        var teams = await calendars.GetTeamsOfStaffAsync([staff], ct);
        return teams.TryGetValue(staff, out var theirs) && theirs.Count > 0 ? theirs.Min() : null;
    }

    /// <summary>
    /// The booking rule (MC5): the clinician belongs to the appointment's team, else a 400 on <c>StaffUserId</c> naming
    /// them and the team.
    /// </summary>
    public static async Task EnsureStaffInTeamAsync(
        Guid teamId,
        Guid staffUserId,
        string? staffUsername,
        ICalendarRepository calendars,
        ITeamRepository teams,
        CancellationToken ct)
    {
        var theirs = await calendars.GetTeamsOfStaffAsync([staffUserId], ct);
        if (theirs.TryGetValue(staffUserId, out var ids) && ids.Contains(teamId))
            return;

        var teamName = (await teams.GetAsync(teamId, ct))?.Name;
        throw new ValidationException([new ValidationFailure("StaffUserId", StaffNotInTeam(staffUsername, teamName))]);
    }
}
