using MedApp.Application.Authentication.Settings.Repositories;
using MedApp.Domain.Authentication;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Data.Repositories;

public sealed class UserSettingsRepository(MedAppDbContext context) : IUserSettingsRepository
{
    public async Task<UserSettings?> GetAsync(Guid userId, CancellationToken cancellationToken) =>
        await context.UserSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);

    public async Task SaveAsync(Guid userId, string json, DateTime utcNow, CancellationToken cancellationToken)
    {
        var existing = await context.UserSettings.FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);
        if (existing is null)
            context.UserSettings.Add(new UserSettings(userId, json, utcNow));
        else
            existing.Replace(json, utcNow);

        await context.SaveChangesAsync(cancellationToken);
    }
}
