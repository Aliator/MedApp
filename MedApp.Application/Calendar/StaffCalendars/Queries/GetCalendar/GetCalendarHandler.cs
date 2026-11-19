using MedApp.Application.Activity.Common;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Teams;
using MedApp.Contracts.Calendar.Responses;
using MedApp.Domain.Activity;
using MediatR;

namespace MedApp.Application.Calendar.StaffCalendars.Queries.GetCalendar;

/// <summary>
/// One person's calendar. Its appointments name their patients, so each patient with one on it is recorded as listed
/// (once per person every few minutes). Asked for without its appointments, it names no patients and records nothing.
/// Behind the viewer's team walls (<see cref="CalendarWalls"/>), another team's appointments and meetings that aren't
/// theirs are busy time only (<c>otherTeamsBusy</c>), naming nobody, so they're not recorded.
/// </summary>
public sealed class GetCalendarHandler(
    ICalendarRepository repository,
    IIdentityReadService identityReadService,
    IActivityLog activityLog,
    ITeamBoundary boundary,
    TimeProvider timeProvider)
    : IRequestHandler<GetCalendarQuery, CalendarResponse>
{
    public async Task<CalendarResponse> Handle(GetCalendarQuery request, CancellationToken cancellationToken)
    {
        var staffUserId = request.StaffUserId ?? request.Actor.UserId;
        var staffUsername = await identityReadService.GetUsernameOrThrowAsync(staffUserId, cancellationToken);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var today = UkTime.DateOf(now);

        var data = await repository.GetCalendarDataAsync(staffUserId, request.From, request.To, cancellationToken);
        if (request.WithoutAppointments)
            data = data with { Appointments = [] };
        var walls = await CalendarWalls.ForAsync(request.Actor.Teams, [data], repository, boundary, cancellationToken);
        if (!request.WithoutAppointments)
            await activityLog.RecordViewsAsync(
                ActivityAction.PatientListed,
                data.Appointments.Where(walls.SeesAppointment).Select(a => a.PatientId),
                cancellationToken,
                details: TeamPicker.ListDetails(null, request.Actor.Teams));
        var defaultWeek = await repository.GetStaffWeekTemplateOnAsync(staffUserId, today, cancellationToken);
        var usernames = await identityReadService.GetUsernamesByIdAsync(CalendarMapper.UserIds(data), cancellationToken);

        return CalendarMapper.ToResponse(
            data,
            staffUsername,
            defaultWeek,
            request.Actor.CanChangeCalendarOf(staffUserId),
            today,
            usernames,
            now,
            request.Actor,
            walls: walls);
    }
}
