using MedApp.Application.Jobs.OperationalPurge;
using MedApp.Domain.Activity;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Data.Repositories;

/// <summary>
/// Deletes old operational data for the operational purge, in batches of <see cref="BatchSize"/> rows (a
/// <c>DELETE TOP (n)</c> each), so no statement holds many locks or a long transaction, and a run cut short has deleted
/// whole batches. A dry run counts instead.
/// </summary>
public sealed class OperationalDataRepository(MedAppDbContext context) : IOperationalDataRepository
{
    /// <summary>Rows deleted by each statement: under SQL Server's lock escalation threshold (5,000 locks).</summary>
    public const int BatchSize = 4000;

    public Task<int> PurgeSessionsAsync(DateTime cutoffUtc, bool dryRun, CancellationToken cancellationToken) =>
        PurgeAsync(
            context.UserSessions.Where(session => session.ExpiresAtUtc < cutoffUtc && session.CreatedAtUtc < cutoffUtc),
            dryRun,
            cancellationToken);

    public Task<int> PurgeActivityAsync(
        IReadOnlyCollection<ActivityAction> actions, DateTime cutoffUtc, bool dryRun, CancellationToken cancellationToken)
    {
        var chosen = actions.ToArray();
        return PurgeAsync(
            context.ActivityEntries.Where(entry => entry.AtUtc < cutoffUtc && chosen.Contains(entry.Action)),
            dryRun,
            cancellationToken);
    }

    public Task<int> PurgeNotificationsAsync(DateTime cutoffUtc, bool dryRun, CancellationToken cancellationToken) =>
        PurgeAsync(
            context.Notifications.Where(notification => notification.CreatedAtUtc < cutoffUtc),
            dryRun,
            cancellationToken);

    public Task<int> PurgeNotepadsAsync(DateTime cutoffUtc, bool dryRun, CancellationToken cancellationToken) =>
        // One statement per batch, with the date in its WHERE: a notepad saved meanwhile no longer matches, so one in use
        // is never deleted.
        PurgeAsync(
            context.Notepads.Where(notepad => notepad.LastUpdatedUtc <= cutoffUtc),
            dryRun,
            cancellationToken);

    public Task<int> PurgeSignInChallengesAsync(DateTime cutoffUtc, bool dryRun, CancellationToken cancellationToken) =>
        // Their emailed codes go with them (a cascading foreign key).
        PurgeAsync(
            context.SignInChallenges.Where(challenge => challenge.ExpiresAtUtc < cutoffUtc),
            dryRun,
            cancellationToken);

    public Task<int> PurgeEmailCodesAsync(DateTime cutoffUtc, bool dryRun, CancellationToken cancellationToken) =>
        PurgeAsync(
            context.EmailCodes.Where(code => code.ExpiresAtUtc < cutoffUtc),
            dryRun,
            cancellationToken);

    private static async Task<int> PurgeAsync<T>(IQueryable<T> old, bool dryRun, CancellationToken cancellationToken)
    {
        if (dryRun)
            return await old.CountAsync(cancellationToken);

        var total = 0;
        int deleted;
        do
        {
            deleted = await old.Take(BatchSize).ExecuteDeleteAsync(cancellationToken);
            total += deleted;
        }
        while (deleted == BatchSize);

        return total;
    }
}
