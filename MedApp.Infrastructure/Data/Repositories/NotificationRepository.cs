using MedApp.Application.Notifications.Repositories;
using MedApp.Domain.Notifications;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Data.Repositories;

public sealed class NotificationRepository(MedAppDbContext context) : INotificationRepository
{
    public async Task AddAsync(Notification notification, CancellationToken cancellationToken)
    {
        var entry = context.Notifications.Add(notification);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Otherwise it stays "added" in the request's DbContext, and the request's next save tries it again.
            entry.State = Microsoft.EntityFrameworkCore.EntityState.Detached;
            throw;
        }
    }

    public async Task<IReadOnlyList<Notification>> GetRecentAsync(Guid userId, int take, CancellationToken cancellationToken) =>
        await context.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAtUtc)
            .ThenBy(n => n.Id)
            .Take(take)
            .ToListAsync(cancellationToken);

    public Task<int> CountUnreadAsync(Guid userId, CancellationToken cancellationToken) =>
        context.Notifications.CountAsync(n => n.UserId == userId && n.ReadAtUtc == null, cancellationToken);

    public async Task<int> MarkReadAsync(Guid userId, Guid? notificationId, DateTime utcNow, CancellationToken cancellationToken)
    {
        var unread = await context.Notifications
            .Where(n => n.UserId == userId && n.ReadAtUtc == null && (notificationId == null || n.Id == notificationId))
            .ToListAsync(cancellationToken);

        foreach (var notification in unread)
            notification.ReadAtUtc = utcNow;

        await context.SaveChangesAsync(cancellationToken);
        return unread.Count;
    }
}
