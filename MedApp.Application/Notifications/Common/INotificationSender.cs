using MedApp.Domain.Notifications;

namespace MedApp.Application.Notifications.Common;

/// <summary>Sends notifications: stores each one, then pushes it to the user's open pages.</summary>
public interface INotificationSender
{
    Task SendAsync(Notification notification, CancellationToken cancellationToken);
}
