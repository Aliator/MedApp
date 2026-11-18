using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Notifications.Common;
using MedApp.Contracts.Calendar.Responses;
using MedApp.Domain.Calendar;
using MediatR;

namespace MedApp.Application.Calendar.ClosedDays.Commands.CreateClosedDay;

/// <summary>
/// Adds a closed day. Anything already on that date (booked appointments, blocks, meetings) stays, and shows flagged;
/// nothing is moved or cancelled. The response says what's there, and each member of staff with something on it is
/// told (<see cref="ClosedDayBookings"/>, decision 75). A weekend can be added too: it changes nothing, but says why
/// the clinic is shut.
/// </summary>
public sealed class CreateClosedDayHandler(
    IClosedDayRepository repository,
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

        if (await repository.GetOnAsync(request.Date, cancellationToken) is { } existing)
            throw new ConflictException(ClosedDayRules.AlreadyClosed(existing));

        var day = new ClosedDay
        {
            Id = Guid.NewGuid(),
            Date = request.Date,
            Name = request.Name.Trim(),
            CreatedAtUtc = now,
            CreatedByUserId = request.Actor.UserId
        };

        await repository.AddAsync([day], cancellationToken);

        var bookings = await calendarRepository.GetBookingsOnAsync(day.Date, cancellationToken);
        foreach (var notification in ClosedDayBookings.Notifications(day.Date, day.Name, bookings, request.Actor, now))
            await notificationSender.SendAsync(notification, cancellationToken);

        var response = ClosedDayMapper.ToResponse(day, today);
        response.Bookings = await ClosedDayBookings.ToResponseAsync(day.Date, bookings, identityReadService, cancellationToken);
        return response;
    }
}
