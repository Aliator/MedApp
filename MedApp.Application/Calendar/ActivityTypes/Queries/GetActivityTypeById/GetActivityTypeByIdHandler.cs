using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Contracts.Calendar.Responses;
using MediatR;

namespace MedApp.Application.Calendar.ActivityTypes.Queries.GetActivityTypeById;

public sealed class GetActivityTypeByIdHandler(ICalendarSetupRepository repository)
    : IRequestHandler<GetActivityTypeByIdQuery, ActivityTypeResponse>
{
    public async Task<ActivityTypeResponse> Handle(GetActivityTypeByIdQuery request, CancellationToken cancellationToken)
    {
        var activityType = await repository.GetActivityTypeOrThrowAsync(request.ActivityTypeId, cancellationToken);
        return await CalendarSetupMapper.ToResponseAsync(activityType, repository, cancellationToken);
    }
}
