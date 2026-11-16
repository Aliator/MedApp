using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Exceptions;
using MediatR;

namespace MedApp.Application.Calendar.ActivityTypes.Commands.DeleteActivityType;

public sealed class DeleteActivityTypeHandler(ICalendarSetupRepository repository)
    : IRequestHandler<DeleteActivityTypeCommand>
{
    public async Task Handle(DeleteActivityTypeCommand request, CancellationToken cancellationToken)
    {
        var activityType = await repository.GetActivityTypeOrThrowAsync(request.ActivityTypeId, cancellationToken);

        var inUse = await repository.GetActivityTypesInUseAsync([activityType.Id], cancellationToken);
        if (inUse.Contains(activityType.Id))
            throw new ConflictException(
                $"The '{activityType.Name}' activity type is used on calendars or in day templates, so it can't be deleted. Archive it instead.");

        await repository.DeleteActivityTypeAsync(activityType.Id, cancellationToken);
    }
}
