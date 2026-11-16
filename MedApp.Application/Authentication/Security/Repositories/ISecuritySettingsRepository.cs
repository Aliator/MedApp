using MedApp.Domain.Authentication;

namespace MedApp.Application.Authentication.Security.Repositories;

/// <summary>The deployment's sign-in rules (<see cref="SecuritySettings"/>), one row.</summary>
public interface ISecuritySettingsRepository
{
    /// <summary>The settings, or the defaults (everything off) if they've never been saved.</summary>
    Task<SecuritySettings> GetAsync(CancellationToken ct);

    /// <summary>Saves the settings, adding the row the first time.</summary>
    Task SaveAsync(SecuritySettings settings, CancellationToken ct);
}
