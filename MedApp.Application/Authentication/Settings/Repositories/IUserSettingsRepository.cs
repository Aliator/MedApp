using MedApp.Domain.Authentication;

namespace MedApp.Application.Authentication.Settings.Repositories;

public interface IUserSettingsRepository
{
    /// <summary>The user's settings, or null if they've never saved any.</summary>
    Task<UserSettings?> GetAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Saves the user's settings, replacing any they had.</summary>
    Task SaveAsync(Guid userId, string json, DateTime utcNow, CancellationToken cancellationToken);
}
