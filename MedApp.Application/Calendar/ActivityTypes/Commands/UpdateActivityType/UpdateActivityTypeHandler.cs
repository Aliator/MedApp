using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Contracts.Calendar.Responses;
using MediatR;

namespace MedApp.Application.Calendar.ActivityTypes.Commands.UpdateActivityType;

public sealed class UpdateActivityTypeHandler(ICalendarSetupRepository repository)
    : IRequestHandler<UpdateActivityTypeCommand, ActivityTypeResponse>
{
    public async Task<ActivityTypeResponse> Handle(UpdateActivityTypeCommand request, CancellationToken cancellationToken)
    {
        var activityType = await repository.GetActivityTypeOrThrowAsync(request.ActivityTypeId, cancellationToken);

        if (request.Name?.Trim() is { } name && name != activityType.Name)
        {
            if (await repository.ActivityTypeNameExistsAsync(name, activityType.Id, cancellationToken))
                throw new ConflictException($"An activity type called '{name}' already exists.");

            activityType.Name = name;
        }

        if (request.Colour is not null)
            activityType.Colour = CalendarColours.Normalise(request.Colour);

        await repository.UpdateActivityTypeAsync(activityType, cancellationToken);

        return await CalendarSetupMapper.ToResponseAsync(activityType, repository, cancellationToken);
    }
}
