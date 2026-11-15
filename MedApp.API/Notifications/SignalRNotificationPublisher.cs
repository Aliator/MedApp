using MedApp.Application.Notifications.Common;
using MedApp.Contracts.Notifications.Responses;
using Microsoft.AspNetCore.SignalR;

namespace MedApp.API.Notifications;

/// <summary>Pushes new notifications to the user's open pages through <see cref="NotificationsHub"/>.</summary>
public sealed class SignalRNotificationPublisher(
    IHubContext<NotificationsHub> hub,
    ILogger<SignalRNotificationPublisher> logger) : INotificationPublisher
{
    public async Task PublishAsync(Guid userId, NotificationResponse notification, CancellationToken cancellationToken)
    {
        try
        {
            await hub.Clients.User(userId.ToString())
                .SendAsync(NotificationsHub.NotificationMethod, notification, cancellationToken);
        }
        catch (Exception exception)
        {
            // Stored already: the user sees it when their pages next load the list.
            logger.LogWarning(exception, "Couldn't push notification {NotificationId} to its user's open pages.", notification.Id);
        }
    }
}
