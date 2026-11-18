using MedApp.Application.Jobs.Repositories;
using MedApp.Domain.Jobs;

namespace MedApp.UnitTests.Application.Jobs.Common;

/// <summary>
/// The scheduled jobs' rows in memory, with the same rules as the SQL statements (one lock stands in for SQL Server's
/// row lock). The integration tests check the real ones against SQL Server.
/// </summary>
public sealed class InMemoryScheduledJobRepository : IScheduledJobRepository
{
    private readonly Lock _lock = new();

    public Dictionary<string, ScheduledJob> Rows { get; } = new(StringComparer.Ordinal);

    /// <summary>Whether renewing finds the lease still held (false: it passed to another runner).</summary>
    public bool RenewalsSucceed { get; set; } = true;

    public int Renewals { get; private set; }

    public Task EnsureAsync(string name, DateTime firstRunUtc, CancellationToken cancellationToken)
    {
        lock (_lock)
            Rows.TryAdd(name, new ScheduledJob { Name = name, NextRunUtc = firstRunUtc });
        return Task.CompletedTask;
    }

    public Task<bool> TryTakeLeaseAsync(
        string name, string holder, DateTime nowUtc, DateTime leaseUntilUtc, bool evenIfNotDue, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            if (!Rows.TryGetValue(name, out var row)
                || (!evenIfNotDue && row.NextRunUtc > nowUtc)
                || (row.LeaseUntilUtc is { } until && until >= nowUtc))
                return Task.FromResult(false);

            row.LeaseHolder = holder;
            row.LeaseUntilUtc = leaseUntilUtc;
            row.LastStartedAtUtc = nowUtc;
            return Task.FromResult(true);
        }
    }

    public Task<bool> RenewLeaseAsync(string name, string holder, DateTime leaseUntilUtc, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            Renewals++;
            if (!RenewalsSucceed || !Rows.TryGetValue(name, out var row) || row.LeaseHolder != holder)
                return Task.FromResult(false);

            row.LeaseUntilUtc = leaseUntilUtc;
            return Task.FromResult(true);
        }
    }

    public Task<ScheduledJob?> CompleteAsync(
        string name,
        string holder,
        ScheduledJobOutcome outcome,
        int? count,
        string? errorType,
        DateTime finishedAtUtc,
        DateTime nextRunUtc,
        CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            if (!Rows.TryGetValue(name, out var row) || row.LeaseHolder != holder)
                return Task.FromResult<ScheduledJob?>(null);

            row.LeaseHolder = null;
            row.LeaseUntilUtc = null;
            row.LastFinishedAtUtc = finishedAtUtc;
            row.NextRunUtc = nextRunUtc;
            row.LastOutcome = outcome;
            row.LastCount = outcome == ScheduledJobOutcome.Succeeded ? count : null;
            switch (outcome)
            {
                case ScheduledJobOutcome.Succeeded:
                    row.ConsecutiveFailures = 0;
                    row.LastErrorType = null;
                    break;
                case ScheduledJobOutcome.Failed:
                    row.ConsecutiveFailures++;
                    row.LastErrorType = errorType;
                    break;
            }

            return Task.FromResult<ScheduledJob?>(Copy(row));
        }
    }

    public Task ReleaseAsync(string name, string holder, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            if (Rows.TryGetValue(name, out var row) && row.LeaseHolder == holder)
            {
                row.LeaseHolder = null;
                row.LeaseUntilUtc = null;
            }
        }

        return Task.CompletedTask;
    }

    public Task<ScheduledJob?> GetAsync(string name, CancellationToken cancellationToken)
    {
        lock (_lock)
            return Task.FromResult(Rows.TryGetValue(name, out var row) ? Copy(row) : null);
    }

    public Task<IReadOnlyList<ScheduledJob>> GetAllAsync(CancellationToken cancellationToken)
    {
        lock (_lock)
            return Task.FromResult<IReadOnlyList<ScheduledJob>>(Rows.Values.Select(Copy).ToList());
    }

    private static ScheduledJob Copy(ScheduledJob row) => new()
    {
        Name = row.Name,
        NextRunUtc = row.NextRunUtc,
        LeaseHolder = row.LeaseHolder,
        LeaseUntilUtc = row.LeaseUntilUtc,
        LastStartedAtUtc = row.LastStartedAtUtc,
        LastFinishedAtUtc = row.LastFinishedAtUtc,
        LastOutcome = row.LastOutcome,
        LastCount = row.LastCount,
        ConsecutiveFailures = row.ConsecutiveFailures,
        LastErrorType = row.LastErrorType,
    };
}
