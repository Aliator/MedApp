using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Contracts.Calendar.Responses;
using MediatR;

namespace MedApp.Application.Calendar.ActivityTypes.Queries.GetActivityTypes;

public sealed class GetActivityTypesHandler(ICalendarSetupRepository repository)
    : IRequestHandler<GetActivityTypesQuery, IReadOnlyList<ActivityTypeResponse>>
{
    public async Task<IReadOnlyList<ActivityTypeResponse>> Handle(GetActivityTypesQuery request, CancellationToken cancellationToken)
    {
        var activityTypes = await repository.GetActivityTypesAsync(request.IncludeArchived, cancellationToken);
        var inUse = await repository.GetActivityTypesInUseAsync(activityTypes.Select(t => t.Id), cancellationToken);

        return activityTypes.Select(t => CalendarSetupMapper.ToResponse(t, inUse.Contains(t.Id))).ToList();
    }
}
