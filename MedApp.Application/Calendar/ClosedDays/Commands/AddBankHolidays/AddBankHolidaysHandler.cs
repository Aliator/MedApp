using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Identity;
using MedApp.Application.Notifications.Common;
using MedApp.Contracts.Calendar.Responses;
using MedApp.Domain.Calendar;
using MediatR;

namespace MedApp.Application.Calendar.ClosedDays.Commands.AddBankHolidays;

/// <summary>
/// Adds the year's bank holidays that are still to come and not already closed. As for one closed day, each added
/// day's response says what was already booked on it, and the staff with something on it are told (decision 75).
/// </summary>
public sealed class AddBankHolidaysHandler(
    IClosedDayRepository repository,
    ICalendarRepository calendarRepository,
    IIdentityReadService identityReadService,
    INotificationSender notificationSender,
    TimeProvider timeProvider)
    : IRequestHandler<AddBankHolidaysCommand, AddBankHolidaysResponse>
{
    /// <summary>The date is closed everywhere already (under any name).</summary>
    public const string AlreadyClosed = "AlreadyClosed";

    /// <summary>The date is before today: past days don't change.</summary>
    public const string Past = "Past";

    public async Task<AddBankHolidaysResponse> Handle(AddBankHolidaysCommand request, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var today = UkTime.DateOf(now);

        var holidays = BankHolidays.EnglandAndWales(request.Year);
        // Dates closed everywhere; a site's closed day doesn't stop the bank holiday closing everywhere.
        var closed = (await repository.GetAsync(holidays[0].Date, holidays[^1].Date, cancellationToken))
            .Where(d => d.SiteId is null)
            .Select(d => d.Date)
            .ToHashSet();

        var response = new AddBankHolidaysResponse { Year = request.Year };
        var added = new List<ClosedDay>();

        foreach (var holiday in holidays)
        {
            if (closed.Contains(holiday.Date))
                response.Skipped.Add(ClosedDayMapper.ToSkipped(holiday, AlreadyClosed));
            else if (holiday.Date < today)
                response.Skipped.Add(ClosedDayMapper.ToSkipped(holiday, Past));
            else
                added.Add(new ClosedDay
                {
                    Id = Guid.NewGuid(),
                    Date = holiday.Date,
                    Name = holiday.Name,
                    CreatedAtUtc = now,
                    CreatedByUserId = request.Actor.UserId
                });
        }

        if (added.Count > 0)
            await repository.AddAsync(added, cancellationToken);

        foreach (var day in added)
        {
            var bookings = await calendarRepository.GetBookingsOnAsync(day.Date, cancellationToken);
            foreach (var notification in ClosedDayBookings.Notifications(day.Date, day.Name, bookings, request.Actor, now))
                await notificationSender.SendAsync(notification, cancellationToken);

            var addedDay = ClosedDayMapper.ToResponse(day, today);
            addedDay.Bookings = await ClosedDayBookings.ToResponseAsync(day.Date, bookings, identityReadService, cancellationToken);
            response.Added.Add(addedDay);
        }

        return response;
    }
}
