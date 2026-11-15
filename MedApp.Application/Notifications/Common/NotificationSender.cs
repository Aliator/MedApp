using MedApp.Application.Notifications.Repositories;
using MedApp.Domain.Notifications;

namespace MedApp.Application.Notifications.Common;

public sealed class NotificationSender(INotificationRepository repository, INotificationPublisher publisher) : INotificationSender
{
    public async Task SendAsync(Notification notification, CancellationToken cancellationToken)
    {
        await repository.AddAsync(notification, cancellationToken);
        await publisher.PublishAsync(notification.UserId, NotificationMapper.ToResponse(notification), cancellationToken);
    }
}
