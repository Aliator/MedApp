using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Contracts.Calendar.Responses;
using MedApp.Domain.Calendar;
using MediatR;

namespace MedApp.Application.Calendar.ActivityTypes.Commands.CreateActivityType;

public sealed class CreateActivityTypeHandler(ICalendarSetupRepository repository)
    : IRequestHandler<CreateActivityTypeCommand, ActivityTypeResponse>
{
    public async Task<ActivityTypeResponse> Handle(CreateActivityTypeCommand request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (await repository.ActivityTypeNameExistsAsync(name, null, cancellationToken))
            throw new ConflictException($"An activity type called '{name}' already exists.");

        var activityType = new CalendarActivityType
        {
            Id = Guid.NewGuid(),
            Name = name,
            Colour = CalendarColours.Normalise(request.Colour)
        };

        await repository.AddActivityTypeAsync(activityType, cancellationToken);

        return CalendarSetupMapper.ToResponse(activityType, inUse: false);
    }
}
