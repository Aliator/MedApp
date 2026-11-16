using MedApp.Application.Authentication.Security.Repositories;
using MedApp.Domain.Authentication;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Data.Repositories;

public sealed class SecuritySettingsRepository(MedAppDbContext context) : ISecuritySettingsRepository
{
    public async Task<SecuritySettings> GetAsync(CancellationToken ct) =>
        await context.SecuritySettings
            .AsNoTracking()
            .FirstOrDefaultAsync(settings => settings.Id == SecuritySettings.SingletonId, ct)
        ?? new SecuritySettings();

    public async Task SaveAsync(SecuritySettings settings, CancellationToken ct)
    {
        var exists = await context.SecuritySettings
            .AnyAsync(existing => existing.Id == SecuritySettings.SingletonId, ct);

        if (exists)
            context.SecuritySettings.Update(settings);
        else
            context.SecuritySettings.Add(settings);

        await context.SaveChangesAsync(ct);
    }
}
