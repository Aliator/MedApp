using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Identity;
using MedApp.Contracts.Calendar.Responses;
using MediatR;

namespace MedApp.Application.Calendar.StaffCalendars.Queries.GetAvailability;

/// <summary>Free time, suggested times and busy times (see <see cref="Availability"/>). It names no patient.</summary>
public sealed class GetAvailabilityHandler(
    ICalendarRepository repository,
    IIdentityReadService identityReadService,
    TimeProvider timeProvider)
    : IRequestHandler<GetAvailabilityQuery, AvailabilityResponse>
{
    public async Task<AvailabilityResponse> Handle(GetAvailabilityQuery request, CancellationToken cancellationToken)
    {
        var staff = await identityReadService.GetStaffOrThrowAsync(request.StaffUserIds, cancellationToken);
        var calendars = await repository.GetCalendarsAsync(staff.Select(s => s.UserId).ToList(), request.From, request.To, cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var days = Availability.FreeTime(calendars, request.From, request.To, now, request.ExceptMeetingId);
        var usernames = staff.ToDictionary(s => s.UserId, s => s.Username);

        return new AvailabilityResponse
        {
            From = request.From,
            To = request.To,
            DurationMinutes = request.DurationMinutes,
            Days = days
                .Select(d => new AvailabilityDayResponse { Date = d.Date, Free = d.Free.Select(MeetingMapper.ToResponse).ToList() })
                .ToList(),
            Suggestions = Availability.Suggestions(days, request.DurationMinutes).Select(MeetingMapper.ToResponse).ToList(),
            Busy = calendars
                .SelectMany(c => CalendarClashes.BusyTimes(c, request.ExceptMeetingId).Select(b => new BusyTimeResponse
                {
                    StaffUserId = c.StaffUserId,
                    Username = usernames[c.StaffUserId],
                    StartUtc = b.StartUtc,
                    EndUtc = b.EndUtc,
                    What = b.What
                }))
                .OrderBy(b => b.StartUtc)
                .ThenBy(b => b.Username, StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }
}
