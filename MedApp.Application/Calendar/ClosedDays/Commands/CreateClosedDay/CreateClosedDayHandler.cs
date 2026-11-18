using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Identity;
using MedApp.Application.Notifications.Common;
using MedApp.Application.Sites.Common;
using MedApp.Application.Sites.Repositories;
using MedApp.Contracts.Calendar.Responses;
using MedApp.Domain.Calendar;
using MediatR;

namespace MedApp.Application.Calendar.ClosedDays.Commands.CreateClosedDay;

/// <summary>
/// Adds a closed day. Anything already on that date (booked appointments, blocks, meetings) stays, and shows flagged;
/// nothing is moved or cancelled. The response says what's there, and each member of staff with something on it is
/// told (<see cref="ClosedDayBookings"/>, decision 75). A weekend can be added too: it changes nothing, but says why
/// the clinic is shut. A site's closed day closes that site's rooms and, once working hours say who works where, the
/// hours of staff working there; until then it closes no one's calendar, so it reports no bookings and tells no one.
/// </summary>
public sealed class CreateClosedDayHandler(
    IClosedDayRepository repository,
    ISiteRepository siteRepository,
    ICalendarRepository calendarRepository,
    IIdentityReadService identityReadService,
    INotificationSender notificationSender,
    TimeProvider timeProvider)
    : IRequestHandler<CreateClosedDayCommand, ClosedDayResponse>
{
    public async Task<ClosedDayResponse> Handle(CreateClosedDayCommand request, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var today = UkTime.DateOf(now);
        ClosedDayRules.EnsureNotPast(request.Date, today, ClosedDayRules.PastCantBeAdded);

        var site = request.SiteId is { } siteId
            ? await siteRepository.GetOpenSiteOrThrowAsync(siteId, cancellationToken)
            : null;

        ClosedDayRules.EnsureNotClosed(await repository.GetOnAsync(request.Date, cancellationToken), site?.Id, site?.Name);

        var day = new ClosedDay
        {
            Id = Guid.NewGuid(),
            Date = request.Date,
            Name = request.Name.Trim(),
            SiteId = site?.Id,
            CreatedAtUtc = now,
            CreatedByUserId = request.Actor.UserId
        };

        await repository.AddAsync([day], cancellationToken);

        if (site is not null)
            return ClosedDayMapper.ToResponse(day, today, site.Name);

        var bookings = await calendarRepository.GetBookingsOnAsync(day.Date, cancellationToken);
        foreach (var notification in ClosedDayBookings.Notifications(day.Date, day.Name, bookings, request.Actor, now))
            await notificationSender.SendAsync(notification, cancellationToken);

        var response = ClosedDayMapper.ToResponse(day, today);
        response.Bookings = await ClosedDayBookings.ToResponseAsync(day.Date, bookings, identityReadService, cancellationToken);
        return response;
    }
}
