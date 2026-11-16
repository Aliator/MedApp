using MedApp.Application.Authentication.TwoFactor.Repositories;
using MedApp.Infrastructure.Common.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Data.Repositories;

public sealed class AccountLockoutRepository(MedAppDbContext context) : IAccountLockoutRepository
{
    public Task<TimeSpan?> RecordFailedCodeAsync(
        Guid userId,
        int maxFailures,
        TimeSpan baseLockout,
        DateTimeOffset now,
        CancellationToken ct) =>
        RecordFailureAsync(userId, maxFailures, previous => CodeLockouts.Duration(baseLockout, previous), countsCodeLockout: true, now, ct);

    public Task<TimeSpan?> RecordFailedPasswordAsync(
        Guid userId,
        int maxFailures,
        TimeSpan lockout,
        DateTimeOffset now,
        CancellationToken ct) =>
        RecordFailureAsync(userId, maxFailures, _ => lockout, countsCodeLockout: false, now, ct);

    private async Task<TimeSpan?> RecordFailureAsync(
        Guid userId,
        int maxFailures,
        Func<int, TimeSpan> lockoutFor,
        bool countsCodeLockout,
        DateTimeOffset now,
        CancellationToken ct)
    {
        // A new concurrency stamp too, so an Identity update already under way with the old count fails rather than
        // writing the old count back.
        await context.Users
            .Where(user => user.Id == userId)
            .ExecuteUpdateAsync(set => set
                .SetProperty(user => user.AccessFailedCount, user => user.AccessFailedCount + 1)
                .SetProperty(user => user.ConcurrencyStamp, Guid.NewGuid().ToString()), ct);
        Forget(userId);

        var account = await context.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new { user.AccessFailedCount, user.LockoutEnabled, user.TwoFactorLockouts })
            .FirstOrDefaultAsync(ct);
        if (account is null || !account.LockoutEnabled || account.AccessFailedCount < maxFailures)
            return null;

        var duration = lockoutFor(account.TwoFactorLockouts);
        var until = now + duration;
        var codeLockouts = countsCodeLockout ? 1 : 0;

        // Only the request whose statement finds the count still at the limit applies the lockout; it sets the count
        // back to 0, so the others' find nothing.
        var locked = await context.Users
            .Where(user => user.Id == userId && user.AccessFailedCount >= maxFailures)
            .ExecuteUpdateAsync(set => set
                .SetProperty(user => user.AccessFailedCount, 0)
                .SetProperty(user => user.LockoutEnd, until)
                .SetProperty(user => user.TwoFactorLockouts, user => user.TwoFactorLockouts + codeLockouts)
                .SetProperty(user => user.ConcurrencyStamp, Guid.NewGuid().ToString()), ct);

        return locked > 0 ? duration : null;
    }

    public async Task ResetAsync(Guid userId, CancellationToken ct)
    {
        await context.Users
            .Where(user => user.Id == userId && (user.AccessFailedCount != 0 || user.TwoFactorLockouts != 0))
            .ExecuteUpdateAsync(set => set
                .SetProperty(user => user.AccessFailedCount, 0)
                .SetProperty(user => user.TwoFactorLockouts, 0)
                .SetProperty(user => user.ConcurrencyStamp, Guid.NewGuid().ToString()), ct);
        Forget(userId);
    }

    public void Forget(Guid userId)
    {
        var entries = context.ChangeTracker.Entries()
            .Where(entry => entry.Entity is ApplicationUser user && user.Id == userId
                            || entry.Entity is IdentityUserToken<Guid> token && token.UserId == userId)
            .ToList();
        foreach (var entry in entries)
            entry.State = EntityState.Detached;
    }
}
