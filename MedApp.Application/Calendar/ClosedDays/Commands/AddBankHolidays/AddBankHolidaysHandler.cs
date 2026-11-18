using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Contracts.Calendar.Responses;
using MedApp.Domain.Calendar;
using MediatR;

namespace MedApp.Application.Calendar.ClosedDays.Commands.AddBankHolidays;

public sealed class AddBankHolidaysHandler(IClosedDayRepository repository, TimeProvider timeProvider)
    : IRequestHandler<AddBankHolidaysCommand, AddBankHolidaysResponse>
{
    /// <summary>The date is in the list already (under any name).</summary>
    public const string AlreadyClosed = "AlreadyClosed";

    /// <summary>The date is before today: past days don't change.</summary>
    public const string Past = "Past";

    public async Task<AddBankHolidaysResponse> Handle(AddBankHolidaysCommand request, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var today = UkTime.DateOf(now);

        var holidays = BankHolidays.EnglandAndWales(request.Year);
        var closed = (await repository.GetAsync(holidays[0].Date, holidays[^1].Date, cancellationToken))
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

        response.Added = added.Select(d => ClosedDayMapper.ToResponse(d, today)).ToList();
        return response;
    }
}
