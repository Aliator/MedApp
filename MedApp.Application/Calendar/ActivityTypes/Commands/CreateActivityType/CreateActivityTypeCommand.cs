using MedApp.Contracts.Calendar.Responses;
using MediatR;

namespace MedApp.Application.Calendar.ActivityTypes.Commands.CreateActivityType;

/// <summary>Adds an activity type: a name (unique) and a colour from the palette.</summary>
public sealed record CreateActivityTypeCommand(string Name, string Colour) : IRequest<ActivityTypeResponse>;
