using MedApp.Application.Notifications.Repositories;
using MedApp.Domain.Notifications;

namespace MedApp.Application.Notifications.Common;

/// <summary>
/// Stores a notification and pushes it to its user, unless it's a kind they've turned off in Account Settings
/// (<see cref="NotificationPreferences"/>; only the optional kinds look their choices up).
/// </summary>
public sealed class NotificationSender(
    INotificationRepository repository,
    INotificationPreferencesRepository preferences,
    INotificationPublisher publisher) : INotificationSender
{
    public async Task SendAsync(Notification notification, CancellationToken cancellationToken)
    {
        if (NotificationPreferences.IsOptional(notification.Kind)
            && await preferences.GetAsync(notification.UserId, cancellationToken) is { } chosen
            && !chosen.Wants(notification.Kind))
            return;

        await repository.AddAsync(notification, cancellationToken);
        await publisher.PublishAsync(notification.UserId, NotificationMapper.ToResponse(notification), cancellationToken);
    }
}
