using MedApp.Contracts.Calendar.Responses;
using MediatR;

namespace MedApp.Application.Calendar.ActivityTypes.Commands.SetActivityTypeArchived;

/// <summary>Archives an activity type (it can't be chosen for new blocks any more) or restores it.</summary>
public sealed record SetActivityTypeArchivedCommand(Guid ActivityTypeId, bool IsArchived) : IRequest<ActivityTypeResponse>;
