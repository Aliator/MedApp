using MedApp.Application.Notifications.Repositories;
using MedApp.Domain.Notifications;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Data.Repositories;

public sealed class NotificationPreferencesRepository(MedAppDbContext context) : INotificationPreferencesRepository
{
    public async Task<NotificationPreferences?> GetAsync(Guid userId, CancellationToken cancellationToken) =>
        await context.NotificationPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

    public async Task SaveAsync(Guid userId, bool pathwayAssigned, DateTime utcNow, CancellationToken cancellationToken)
    {
        var existing = await context.NotificationPreferences.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        if (existing is null)
        {
            context.NotificationPreferences.Add(new NotificationPreferences
            {
                UserId = userId,
                PathwayAssigned = pathwayAssigned,
                UpdatedAtUtc = utcNow
            });
        }
        else
        {
            existing.PathwayAssigned = pathwayAssigned;
            existing.UpdatedAtUtc = utcNow;
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
