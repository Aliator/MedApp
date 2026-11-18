using MedApp.Application.Jobs.Repositories;
using MedApp.Domain.Jobs;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Data.Repositories;

/// <summary>
/// The scheduled jobs' rows. Each change is one statement on the job's key, so SQL Server's row lock decides between two
/// copies of the API racing for a job: the second UPDATE waits for the first, then finds the lease taken and changes
/// nothing.
/// </summary>
public sealed class ScheduledJobRepository(MedAppDbContext context) : IScheduledJobRepository
{
    public async Task EnsureAsync(string name, DateTime firstRunUtc, CancellationToken cancellationToken) =>
        // UPDLOCK and HOLDLOCK hold the key range until the insert, so two copies adding the same new job can't both
        // pass the check (one waits, then finds the row).
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO [ScheduledJobs] ([Name], [NextRunUtc], [ConsecutiveFailures])
             SELECT {name}, {firstRunUtc}, 0
             WHERE NOT EXISTS (SELECT 1 FROM [ScheduledJobs] WITH (UPDLOCK, HOLDLOCK) WHERE [Name] = {name})
             """,
            cancellationToken);

    public async Task<bool> TryTakeLeaseAsync(
        string name, string holder, DateTime nowUtc, DateTime leaseUntilUtc, bool evenIfNotDue, CancellationToken cancellationToken) =>
        await context.ScheduledJobs
            .Where(job => job.Name == name
                          && (evenIfNotDue || job.NextRunUtc <= nowUtc)
                          && (job.LeaseUntilUtc == null || job.LeaseUntilUtc < nowUtc))
            .ExecuteUpdateAsync(set => set
                    .SetProperty(job => job.LeaseHolder, holder)
                    .SetProperty(job => job.LeaseUntilUtc, leaseUntilUtc)
                    .SetProperty(job => job.LastStartedAtUtc, nowUtc),
                cancellationToken) > 0;

    public async Task<bool> RenewLeaseAsync(
        string name, string holder, DateTime leaseUntilUtc, CancellationToken cancellationToken) =>
        await context.ScheduledJobs
            .Where(job => job.Name == name && job.LeaseHolder == holder && job.LeaseUntilUtc != null)
            .ExecuteUpdateAsync(set => set.SetProperty(job => job.LeaseUntilUtc, leaseUntilUtc), cancellationToken) > 0;

    public async Task<ScheduledJob?> CompleteAsync(
        string name,
        string holder,
        ScheduledJobOutcome outcome,
        int? count,
        string? errorType,
        DateTime finishedAtUtc,
        DateTime nextRunUtc,
        CancellationToken cancellationToken)
    {
        var mine = context.ScheduledJobs
            .Where(job => job.Name == name && job.LeaseHolder == holder && job.LeaseUntilUtc != null);

        var changed = outcome switch
        {
            ScheduledJobOutcome.Succeeded => await mine.ExecuteUpdateAsync(set => set
                    .SetProperty(job => job.LeaseHolder, (string?)null)
                    .SetProperty(job => job.LeaseUntilUtc, (DateTime?)null)
                    .SetProperty(job => job.LastFinishedAtUtc, finishedAtUtc)
                    .SetProperty(job => job.NextRunUtc, nextRunUtc)
                    .SetProperty(job => job.LastOutcome, outcome)
                    .SetProperty(job => job.LastCount, count)
                    .SetProperty(job => job.ConsecutiveFailures, 0)
                    .SetProperty(job => job.LastErrorType, (string?)null),
                cancellationToken),
            ScheduledJobOutcome.Failed => await mine.ExecuteUpdateAsync(set => set
                    .SetProperty(job => job.LeaseHolder, (string?)null)
                    .SetProperty(job => job.LeaseUntilUtc, (DateTime?)null)
                    .SetProperty(job => job.LastFinishedAtUtc, finishedAtUtc)
                    .SetProperty(job => job.NextRunUtc, nextRunUtc)
                    .SetProperty(job => job.LastOutcome, outcome)
                    .SetProperty(job => job.LastCount, (int?)null)
                    .SetProperty(job => job.ConsecutiveFailures, job => job.ConsecutiveFailures + 1)
                    .SetProperty(job => job.LastErrorType, errorType),
                cancellationToken),
            _ => await mine.ExecuteUpdateAsync(set => set
                    .SetProperty(job => job.LeaseHolder, (string?)null)
                    .SetProperty(job => job.LeaseUntilUtc, (DateTime?)null)
                    .SetProperty(job => job.LastFinishedAtUtc, finishedAtUtc)
                    .SetProperty(job => job.NextRunUtc, nextRunUtc)
                    .SetProperty(job => job.LastOutcome, outcome)
                    .SetProperty(job => job.LastCount, (int?)null),
                cancellationToken)
        };

        return changed > 0 ? await GetAsync(name, cancellationToken) : null;
    }

    public async Task ReleaseAsync(string name, string holder, CancellationToken cancellationToken) =>
        await context.ScheduledJobs
            .Where(job => job.Name == name && job.LeaseHolder == holder)
            .ExecuteUpdateAsync(set => set
                    .SetProperty(job => job.LeaseHolder, (string?)null)
                    .SetProperty(job => job.LeaseUntilUtc, (DateTime?)null),
                cancellationToken);

    public Task<ScheduledJob?> GetAsync(string name, CancellationToken cancellationToken) =>
        context.ScheduledJobs.AsNoTracking().FirstOrDefaultAsync(job => job.Name == name, cancellationToken);

    public async Task<IReadOnlyList<ScheduledJob>> GetAllAsync(CancellationToken cancellationToken) =>
        await context.ScheduledJobs.AsNoTracking().OrderBy(job => job.Name).ToListAsync(cancellationToken);
}
