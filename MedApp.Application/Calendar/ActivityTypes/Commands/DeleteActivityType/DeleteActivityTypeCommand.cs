using MediatR;

namespace MedApp.Application.Calendar.ActivityTypes.Commands.DeleteActivityType;

/// <summary>Deletes an activity type no block or template block has used; one that has been used is archived instead.</summary>
public sealed record DeleteActivityTypeCommand(Guid ActivityTypeId) : IRequest;
