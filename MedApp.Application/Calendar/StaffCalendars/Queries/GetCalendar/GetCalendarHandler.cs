using MedApp.Application.Activity.Common;
using MedApp.Application.Appointments.Common;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Identity;
using MedApp.Contracts.Calendar.Responses;
using MedApp.Domain.Activity;
using MediatR;

namespace MedApp.Application.Calendar.StaffCalendars.Queries.GetCalendar;

/// <summary>
/// One person's calendar. Its appointments name their patients, so each patient with one on it is recorded as listed
/// (once per person every few minutes).
/// </summary>
public sealed class GetCalendarHandler(
    ICalendarRepository repository,
    IIdentityReadService identityReadService,
    IActivityLog activityLog,
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
        await activityLog.RecordViewsAsync(ActivityAction.PatientListed, data.Appointments.Select(a => a.PatientId), cancellationToken);
        var defaultWeek = await repository.GetStaffWeekTemplateOnAsync(staffUserId, today, cancellationToken);
        var usernames = await identityReadService.GetUsernamesByIdAsync(
            data.Appointments.SelectMany(AppointmentMapper.UserIds).Distinct(), cancellationToken);

        return CalendarMapper.ToResponse(
            data,
            staffUsername,
            defaultWeek,
            request.Actor.CanChangeCalendarOf(staffUserId),
            today,
            usernames,
            now);
    }
}
