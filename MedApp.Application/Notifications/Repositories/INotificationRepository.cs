using MedApp.Domain.Notifications;

namespace MedApp.Application.Notifications.Repositories;

public interface INotificationRepository
{
    Task AddAsync(Notification notification, CancellationToken cancellationToken);

    /// <summary>The user's newest notifications, newest first.</summary>
    Task<IReadOnlyList<Notification>> GetRecentAsync(Guid userId, int take, CancellationToken cancellationToken);

    Task<int> CountUnreadAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Marks the user's unread notifications read: the one given, or all of them. Returns how many it marked (0 for
    /// one that isn't theirs, or was already read).
    /// </summary>
    Task<int> MarkReadAsync(Guid userId, Guid? notificationId, DateTime utcNow, CancellationToken cancellationToken);
}
