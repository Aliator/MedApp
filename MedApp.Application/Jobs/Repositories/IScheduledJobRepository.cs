using MedApp.Domain.Jobs;

namespace MedApp.Application.Jobs.Repositories;

/// <summary>
/// The scheduled jobs' rows and their leases. Every change is one SQL statement, so two copies of the API racing for
/// the same job can't both win.
/// </summary>
public interface IScheduledJobRepository
{
    /// <summary>Adds the job's row, due at <paramref name="firstRunUtc"/>, unless it has one already (safe to race).</summary>
    Task EnsureAsync(string name, DateTime firstRunUtc, CancellationToken cancellationToken);

    /// <summary>
    /// Takes the job's lease for <paramref name="holder"/> until <paramref name="leaseUntilUtc"/>, in one statement, if
    /// nobody holds it (or theirs has ended) and, unless <paramref name="evenIfNotDue"/>, it's due. Records the start.
    /// True when this holder now has it.
    /// </summary>
    Task<bool> TryTakeLeaseAsync(
        string name, string holder, DateTime nowUtc, DateTime leaseUntilUtc, bool evenIfNotDue, CancellationToken cancellationToken);

    /// <summary>Moves the lease's end on, if <paramref name="holder"/> still holds it. False when it doesn't.</summary>
    Task<bool> RenewLeaseAsync(string name, string holder, DateTime leaseUntilUtc, CancellationToken cancellationToken);

    /// <summary>
    /// Records how the run ended and when it's next due, and gives up the lease, if <paramref name="holder"/> still
    /// holds it: a success sets the failures back to 0 and keeps the count, a failure adds one and keeps the error's
    /// type, a skip changes neither. Returns the row afterwards, or null when the lease had passed to someone else.
    /// </summary>
    Task<ScheduledJob?> CompleteAsync(
        string name,
        string holder,
        ScheduledJobOutcome outcome,
        int? count,
        string? errorType,
        DateTime finishedAtUtc,
        DateTime nextRunUtc,
        CancellationToken cancellationToken);

    /// <summary>Gives up the lease without recording a run (the API is stopping), so another runner can take the job.</summary>
    Task ReleaseAsync(string name, string holder, CancellationToken cancellationToken);

    Task<ScheduledJob?> GetAsync(string name, CancellationToken cancellationToken);

    Task<IReadOnlyList<ScheduledJob>> GetAllAsync(CancellationToken cancellationToken);
}
