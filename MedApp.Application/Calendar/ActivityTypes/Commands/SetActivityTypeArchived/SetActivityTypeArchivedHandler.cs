using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Contracts.Calendar.Responses;
using MediatR;

namespace MedApp.Application.Calendar.ActivityTypes.Commands.SetActivityTypeArchived;

public sealed class SetActivityTypeArchivedHandler(ICalendarSetupRepository repository)
    : IRequestHandler<SetActivityTypeArchivedCommand, ActivityTypeResponse>
{
    public async Task<ActivityTypeResponse> Handle(SetActivityTypeArchivedCommand request, CancellationToken cancellationToken)
    {
        var activityType = await repository.GetActivityTypeOrThrowAsync(request.ActivityTypeId, cancellationToken);

        if (activityType.IsArchived != request.IsArchived)
        {
            activityType.IsArchived = request.IsArchived;
            await repository.UpdateActivityTypeAsync(activityType, cancellationToken);
        }

        return await CalendarSetupMapper.ToResponseAsync(activityType, repository, cancellationToken);
    }
}
