using MedApp.Domain.Notifications;

namespace MedApp.Application.Notifications.Repositories;

public interface INotificationPreferencesRepository
{
    /// <summary>The user's choices, read-only, or null if they've never made any (they get everything).</summary>
    Task<NotificationPreferences?> GetAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Saves the user's choices, replacing any they had.</summary>
    Task SaveAsync(Guid userId, bool procedureAssigned, DateTime utcNow, CancellationToken cancellationToken);
}
