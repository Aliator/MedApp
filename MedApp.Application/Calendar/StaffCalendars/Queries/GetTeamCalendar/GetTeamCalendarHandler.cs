using MedApp.Application.Activity.Common;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Teams;
using MedApp.Application.Teams.Repositories;
using MedApp.Contracts.Calendar.Responses;
using MedApp.Domain.Activity;
using MedApp.Domain.Teams;
using MediatR;

namespace MedApp.Application.Calendar.StaffCalendars.Queries.GetTeamCalendar;

/// <summary>
/// A team's day (MC11): any team, for anyone (the light demo's <c>GET /api/calendar/team</c>). Its members' appointments
/// name patients, so each patient with one the viewer sees is recorded as listed, as one person's calendar does; another
/// team's appointments, and meetings that aren't the viewer's, are busy time only (<see cref="CalendarWalls"/>).
/// </summary>
public sealed class GetTeamCalendarHandler(
    ICalendarRepository repository,
    IClosedDayRepository closedDays,
    IIdentityReadService identityReadService,
    IActivityLog activityLog,
    ITeamRepository teams,
    ITeamBoundary boundary,
    TimeProvider timeProvider)
    : IRequestHandler<GetTeamCalendarQuery, TeamCalendarResponse>
{
    public async Task<TeamCalendarResponse> Handle(GetTeamCalendarQuery request, CancellationToken cancellationToken)
    {
        var scope = request.Actor.Teams;
        var team = await ChooseTeamAsync(request.TeamId, scope, cancellationToken);

        // Everyone in the team with a staff role, by username; those asked for, of them, or all.
        var memberIds = team is null ? [] : (await repository.GetTeamMemberIdsAsync(team.Id, cancellationToken)).ToHashSet();
        var inTeam = (await identityReadService.GetStaffAsync(cancellationToken))
            .Where(s => memberIds.Contains(s.UserId))
            .OrderBy(s => s.Username, StringComparer.OrdinalIgnoreCase)
            .ThenBy(s => s.UserId)
            .ToList();
        var asked = request.StaffUserIds.ToHashSet();
        var staff = asked.Count == 0 ? inTeam : inTeam.Where(s => asked.Contains(s.UserId)).ToList();

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var today = UkTime.DateOf(now);

        var calendars = await repository.GetCalendarsAsync(staff.Select(s => s.UserId).ToList(), request.Date, request.Date, cancellationToken);
        var walls = await CalendarWalls.ForAsync(scope, calendars, repository, boundary, cancellationToken);
        await activityLog.RecordViewsAsync(
            ActivityAction.PatientListed,
            calendars.SelectMany(c => c.Appointments).Where(walls.SeesAppointment).Select(a => a.PatientId),
            cancellationToken,
            details: TeamPicker.ListDetails(team?.Id, scope));

        var usernames = await identityReadService.GetUsernamesByIdAsync(
            calendars.SelectMany(CalendarMapper.UserIds).Distinct(), cancellationToken);
        var byId = calendars.ToDictionary(c => c.StaffUserId);
        // One grid for everyone, so the columns' rows line up: it fits everyone's hours and everything they have on.
        var window = GridWindow.Of(calendars);
        var weeks = (await repository.GetStaffWeekTemplatesOnAsync(today, cancellationToken))
            .GroupBy(w => w.StaffUserId)
            .ToDictionary(g => g.Key, g => g.MaxBy(w => w.ValidFrom));

        return new TeamCalendarResponse
        {
            Date = request.Date,
            TeamId = team?.Id,
            TeamName = team?.Name,
            // The day itself, closed or not (the "Everyone free" column), whoever is shown, even nobody.
            Day = CalendarMapper.ToResponse(CalendarProjection.TeamDay(
                calendars,
                request.Date,
                today,
                new ClosedDaySet(await closedDays.GetAsync(request.Date, request.Date, cancellationToken)),
                window)),
            Members = staff
                .Select(s =>
                {
                    var data = byId[s.UserId];
                    return CalendarMapper.ToResponse(
                        data,
                        s.Username,
                        CalendarProjection.DefaultWeekTemplateOn(data, today),
                        request.Actor.CanChangeCalendarOf(s.UserId),
                        today,
                        usernames,
                        now,
                        request.Actor,
                        window,
                        walls);
                })
                .ToList(),
            // Free time counts everything on their calendars, every team's (busy time names nothing).
            EveryoneFree = staff.Count == 0
                ? []
                : Availability.FreeTime(calendars, request.Date, request.Date, now)
                    .SelectMany(d => d.Free)
                    .Select(MeetingMapper.ToResponse)
                    .ToList(),
            TeamMembers = inTeam.Select(s => CalendarMapper.ToResponse(s, weeks.GetValueOrDefault(s.UserId))).ToList()
        };
    }

    /// <summary>
    /// The team asked for (any, archived too; a 404 if there's none), else the viewer's first team by name (of those not
    /// archived), else the first team by name.
    /// </summary>
    private async Task<Team?> ChooseTeamAsync(Guid? teamId, TeamScope scope, CancellationToken ct)
    {
        if (teamId is { } id)
            return await teams.GetAsync(id, ct) ?? throw new NotFoundException($"Team '{id}' was not found.");

        var open = (await teams.GetAllAsync(includeArchived: false, ct))
            .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(t => t.Id)
            .ToList();
        return open.FirstOrDefault(t => scope.IsMember(t.Id)) ?? open.FirstOrDefault();
    }
}
