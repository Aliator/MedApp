using MedApp.Domain.Activity;

namespace MedApp.Application.Jobs.OperationalPurge;

/// <summary>
/// Deletes operational data older than a cutoff, for the operational purge. Each method deletes in batches, one
/// statement each, so a run cut short has deleted whole batches and the next run carries on; with <c>dryRun</c> it only
/// counts. Each returns how many rows it deleted, or would delete. Never patient records.
/// </summary>
public interface IOperationalDataRepository
{
    /// <summary>Sessions created before <paramref name="cutoffUtc"/>, and expired before it.</summary>
    Task<int> PurgeSessionsAsync(DateTime cutoffUtc, bool dryRun, CancellationToken cancellationToken);

    /// <summary>Activity log entries of one of <paramref name="actions"/> recorded before <paramref name="cutoffUtc"/>.</summary>
    Task<int> PurgeActivityAsync(
        IReadOnlyCollection<ActivityAction> actions, DateTime cutoffUtc, bool dryRun, CancellationToken cancellationToken);

    /// <summary>Notifications made before <paramref name="cutoffUtc"/>, read or not.</summary>
    Task<int> PurgeNotificationsAsync(DateTime cutoffUtc, bool dryRun, CancellationToken cancellationToken);

    /// <summary>Notepads not changed since <paramref name="cutoffUtc"/> (at or before it).</summary>
    Task<int> PurgeNotepadsAsync(DateTime cutoffUtc, bool dryRun, CancellationToken cancellationToken);

    /// <summary>Sign-in challenges that expired before <paramref name="cutoffUtc"/> (their emailed codes go with them).</summary>
    Task<int> PurgeSignInChallengesAsync(DateTime cutoffUtc, bool dryRun, CancellationToken cancellationToken);

    /// <summary>Emailed two-factor codes that expired before <paramref name="cutoffUtc"/>.</summary>
    Task<int> PurgeEmailCodesAsync(DateTime cutoffUtc, bool dryRun, CancellationToken cancellationToken);
}
