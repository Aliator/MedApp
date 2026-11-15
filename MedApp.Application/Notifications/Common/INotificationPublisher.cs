using MedApp.Contracts.Notifications.Responses;

namespace MedApp.Application.Notifications.Common;

/// <summary>
/// Pushes a new notification to the user's open pages as it happens (the API does it over SignalR). Best effort: it
/// never throws, since the notification is already stored and shows when they next look.
/// </summary>
public interface INotificationPublisher
{
    Task PublishAsync(Guid userId, NotificationResponse notification, CancellationToken cancellationToken);
}
