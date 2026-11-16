using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MedApp.Infrastructure.Common.Identity;

/// <summary>
/// Warns at startup, outside Development, when the data protection keys aren't wrapped
/// (<c>DataProtection:KeyVaultKeyUri</c> unset): they're then stored in the database as they are, beside the security
/// stamps they seal tokens with, so anyone with a copy of the database could make a password reset token for any account.
/// Acceptable for a local cluster with synthetic data; not for real data.
/// </summary>
public sealed class DataProtectionKeyCheck(
    IConfiguration configuration,
    IHostEnvironment environment,
    ILogger<DataProtectionKeyCheck> logger)
    : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment() && !IsWrapped(configuration))
            logger.LogWarning(
                "The data protection keys aren't wrapped ({Setting} isn't set), so they're stored in the database as they are. Set it before holding real data.",
                Extensions.ServiceExtensions.DataProtectionKeyVaultKeySetting);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Whether a key to wrap them with is configured.</summary>
    public static bool IsWrapped(IConfiguration configuration) =>
        !string.IsNullOrWhiteSpace(configuration[Extensions.ServiceExtensions.DataProtectionKeyVaultKeySetting]);
}
