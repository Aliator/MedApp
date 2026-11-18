using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Contracts.Calendar.Responses;
using MedApp.Domain.Calendar;
using MediatR;

namespace MedApp.Application.Calendar.ClosedDays.Commands.CreateClosedDay;

/// <summary>
/// Adds a closed day. Anything already on that date (booked appointments, blocks, meetings) stays, and shows flagged;
/// nothing is moved or cancelled. A weekend can be added too: it changes nothing, but says why the clinic is shut.
/// </summary>
public sealed class CreateClosedDayHandler(IClosedDayRepository repository, TimeProvider timeProvider)
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
        return ClosedDayMapper.ToResponse(day, today);
    }
}
