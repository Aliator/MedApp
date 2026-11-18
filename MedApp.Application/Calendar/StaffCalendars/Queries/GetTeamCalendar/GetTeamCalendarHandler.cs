using MedApp.Application.Activity.Common;
using MedApp.Application.Appointments.Common;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Identity;
using MedApp.Contracts.Calendar.Responses;
using MedApp.Domain.Activity;
using MediatR;

namespace MedApp.Application.Calendar.StaffCalendars.Queries.GetTeamCalendar;

/// <summary>
/// Several people's day. Their appointments name patients, so each patient with one on it is recorded as listed, as
/// one person's calendar does.
/// </summary>
public sealed class GetTeamCalendarHandler(
    ICalendarRepository repository,
    IIdentityReadService identityReadService,
    IActivityLog activityLog,
    TimeProvider timeProvider)
    : IRequestHandler<GetTeamCalendarQuery, TeamCalendarResponse>
{
    public async Task<TeamCalendarResponse> Handle(GetTeamCalendarQuery request, CancellationToken cancellationToken)
    {
        var staff = request.StaffUserIds.Count == 0
            ? await identityReadService.GetStaffAsync(cancellationToken)
            : await identityReadService.GetStaffOrThrowAsync(request.StaffUserIds, cancellationToken);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var today = UkTime.DateOf(now);

        var calendars = await repository.GetCalendarsAsync(staff.Select(s => s.UserId).ToList(), request.Date, request.Date, cancellationToken);
        await activityLog.RecordViewsAsync(
            ActivityAction.PatientListed, calendars.SelectMany(c => c.Appointments).Select(a => a.PatientId), cancellationToken);

        var usernames = await identityReadService.GetUsernamesByIdAsync(
            calendars.SelectMany(CalendarMapper.UserIds).Distinct(), cancellationToken);
        var byId = calendars.ToDictionary(c => c.StaffUserId);

        return new TeamCalendarResponse
        {
            Date = request.Date,
            Day = CalendarMapper.ToResponse(CalendarProjection.Days(CalendarData.Empty(Guid.Empty, request.Date, request.Date), today)[0]),
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
                        request.Actor);
                })
                .ToList(),
            EveryoneFree = Availability.FreeTime(calendars, request.Date, request.Date, now)
                .SelectMany(d => d.Free)
                .Select(MeetingMapper.ToResponse)
                .ToList()
        };
    }
}
