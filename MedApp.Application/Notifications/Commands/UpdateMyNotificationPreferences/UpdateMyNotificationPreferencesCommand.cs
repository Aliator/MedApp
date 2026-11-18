using MedApp.Contracts.Notifications.Responses;
using MediatR;

namespace MedApp.Application.Notifications.Commands.UpdateMyNotificationPreferences;

/// <summary>Saves which notifications the signed-in user wants, replacing what they chose before.</summary>
public sealed record UpdateMyNotificationPreferencesCommand(Guid UserId, bool PathwayAssigned)
    : IRequest<NotificationPreferencesResponse>;
