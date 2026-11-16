using MedApp.Contracts.Calendar.Responses;
using MediatR;

namespace MedApp.Application.Calendar.ActivityTypes.Commands.UpdateActivityType;

/// <summary>Edits an activity type; null fields are left as they are. Blocks that have it show the change.</summary>
public sealed record UpdateActivityTypeCommand(Guid ActivityTypeId, string? Name, string? Colour)
    : IRequest<ActivityTypeResponse>;
