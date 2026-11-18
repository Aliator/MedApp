using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Contracts.Calendar.Responses;
using MediatR;

namespace MedApp.Application.Calendar.ClosedDays.Queries.GetClosedDays;

public sealed class GetClosedDaysHandler(IClosedDayRepository repository, TimeProvider timeProvider)
    : IRequestHandler<GetClosedDaysQuery, IReadOnlyList<ClosedDayResponse>>
{
    public async Task<IReadOnlyList<ClosedDayResponse>> Handle(GetClosedDaysQuery request, CancellationToken cancellationToken)
    {
        var today = UkTime.Today(timeProvider);
        var days = await repository.GetAsync(request.From, request.To, cancellationToken);
        return days.Select(d => ClosedDayMapper.ToResponse(d, today)).ToList();
    }
}
