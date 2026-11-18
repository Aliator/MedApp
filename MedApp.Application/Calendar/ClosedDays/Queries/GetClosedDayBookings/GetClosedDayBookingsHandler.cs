using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Identity;
using MedApp.Contracts.Calendar.Responses;
using MediatR;

namespace MedApp.Application.Calendar.ClosedDays.Queries.GetClosedDayBookings;

public sealed class GetClosedDayBookingsHandler(ICalendarRepository calendarRepository, IIdentityReadService identityReadService)
    : IRequestHandler<GetClosedDayBookingsQuery, DayBookingsResponse>
{
    public async Task<DayBookingsResponse> Handle(GetClosedDayBookingsQuery request, CancellationToken cancellationToken)
    {
        var bookings = await calendarRepository.GetBookingsOnAsync(request.Date, cancellationToken);
        return await ClosedDayBookings.ToResponseAsync(request.Date, bookings, identityReadService, cancellationToken);
    }
}
