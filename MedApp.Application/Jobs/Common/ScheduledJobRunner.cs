using System.Diagnostics;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Jobs.Repositories;
using MedApp.Application.Notifications.Common;
using MedApp.Domain.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MedApp.Application.Jobs.Common;

/// <summary>Runs scheduled jobs: those that are due (on a timer), or one now.</summary>
public interface IScheduledJobRunner
{
    /// <summary>Runs every job that's due and whose lease this runner can take, one after another.</summary>
    Task RunDueAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Runs one job now, due or not (Run now, the <c>run-job</c> command, <c>medapp-dev</c>'s <c>run_job</c>, tests),
    /// and returns its row afterwards. A 404 for an unknown job; a 409 when it's switched off or running already.
    /// </summary>
    Task<ScheduledJob> RunNowAsync(string name, CancellationToken cancellationToken);
}

/// <summary>
/// Runs the scheduled jobs (<see cref="IScheduledJob"/>). Before running one it takes the job's lease in SQL Server, in
/// one statement (<see cref="IScheduledJobRepository.TryTakeLeaseAsync"/>), so exactly one runner, across every copy of
/// the API, runs each job at a time. The lease lasts <see cref="LeaseDuration"/> and is renewed while the job runs; a
/// runner that dies leaves it to end, after which another takes the job. Each run is in a scope of its own, marked as a
/// job's (<see cref="ScheduledJobScope"/>), and records its outcome, count and the exception's type (never its message).
/// After <see cref="FailuresBeforeTelling"/> failures in a row, the holders of <see cref="Permission.ManageSetup"/> are told once.
/// </summary>
public sealed class ScheduledJobRunner(
    IServiceScopeFactory scopes,
    TimeProvider timeProvider,
    IOptions<JobsOptions> options,
    ILogger<ScheduledJobRunner> logger) : IScheduledJobRunner
{
    /// <summary>How long a lease lasts unless it's renewed.</summary>
    public static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(10);

    /// <summary>How often a running job's lease is renewed: well inside <see cref="LeaseDuration"/>.</summary>
    public static readonly TimeSpan RenewEvery = TimeSpan.FromMinutes(3);

    /// <summary>Failures in a row after which the holders of ManageSetup are told (once, until it succeeds).</summary>
    public const int FailuresBeforeTelling = 3;

    /// <summary>This runner's name in a lease: the machine (the pod), the process and a random part, so each is unique.</summary>
    public string Holder { get; } = MakeHolder();

    public async Task RunDueAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IScheduledJobRepository>();
        var jobs = scope.ServiceProvider.GetServices<IScheduledJob>()
            .Select(job => (job.Name, job.Schedule))
            .ToList();

        var now = Now();
        foreach (var (name, schedule) in jobs)
            await repository.EnsureAsync(name, schedule.FirstRun(now), cancellationToken);

        foreach (var (name, schedule) in jobs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var startedAt = Now();
            if (await repository.TryTakeLeaseAsync(
                    name, Holder, startedAt, startedAt + LeaseDuration, evenIfNotDue: false, cancellationToken))
                await RunLeasedAsync(name, schedule, startedAt, cancellationToken);
        }
    }

    public async Task<ScheduledJob> RunNowAsync(string name, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var job = scope.ServiceProvider.GetServices<IScheduledJob>().FirstOrDefault(job => job.Name == name)
                  ?? throw new NotFoundException($"There's no scheduled job called {name}.");
        if (!options.Value.IsJobEnabled(name))
            throw new ConflictException($"{job.Title} is switched off (Jobs:{name}:Enabled), so it can't be run.");

        var repository = scope.ServiceProvider.GetRequiredService<IScheduledJobRepository>();
        var startedAt = Now();
        await repository.EnsureAsync(name, job.Schedule.FirstRun(startedAt), cancellationToken);
        if (!await repository.TryTakeLeaseAsync(
                name, Holder, startedAt, startedAt + LeaseDuration, evenIfNotDue: true, cancellationToken))
            throw new ConflictException($"{job.Title} is running already. Try again when it has finished.");

        return await RunLeasedAsync(name, job.Schedule, startedAt, cancellationToken)
               ?? await repository.GetAsync(name, cancellationToken)
               ?? throw new NotFoundException($"There's no scheduled job called {name}.");
    }

    /// <summary>
    /// Runs a job whose lease this runner holds, renewing the lease while it runs, then records the outcome and gives
    /// the lease up. Returns the job's row afterwards, or null if the lease was lost meanwhile (nothing recorded).
    /// </summary>
    private async Task<ScheduledJob?> RunLeasedAsync(
        string name, JobSchedule schedule, DateTime startedAt, CancellationToken cancellationToken)
    {
        if (!options.Value.IsJobEnabled(name))
        {
            logger.LogInformation("Scheduled job {Job} is switched off, so it was skipped.", name);
            return await CompleteAsync(
                name, ScheduledJobOutcome.Skipped, null, null, schedule.NextAfter(startedAt), cancellationToken);
        }

        using var run = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var stopRenewing = new CancellationTokenSource();
        var renewal = RenewWhileRunningAsync(name, run, stopRenewing.Token);
        var stopwatch = Stopwatch.StartNew();

        int? count = null;
        Exception? failure = null;
        try
        {
            count = await RunInScopeAsync(name, startedAt, run.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Stopping (or the request gone): give the lease up unrecorded, so a runner takes the job again soon.
            await StopRenewingAsync(stopRenewing, renewal);
            await ReleaseQuietlyAsync(name);
            throw;
        }
        catch (OperationCanceledException) when (run.IsCancellationRequested)
        {
            await StopRenewingAsync(stopRenewing, renewal);
            logger.LogWarning("Scheduled job {Job} stopped: its lease passed to another runner.", name);
            return null;
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        await StopRenewingAsync(stopRenewing, renewal);
        var finishedAt = Now();
        var next = schedule.NextRunAfter(startedAt, finishedAt, failed: failure is not null);

        if (failure is null)
        {
            logger.LogInformation(
                "Scheduled job {Job} succeeded in {Seconds:0.0} s: {Count} items.", name, stopwatch.Elapsed.TotalSeconds, count);
            return await CompleteAsync(name, ScheduledJobOutcome.Succeeded, count, null, next, CancellationToken.None);
        }

        // The exception's type and stack, never its message: a message can quote data (a SQL Server message holds the
        // value that broke a unique index).
        var errorType = ErrorType(failure);
        var row = await CompleteAsync(name, ScheduledJobOutcome.Failed, null, errorType, next, CancellationToken.None);
        logger.LogError(
            "Scheduled job {Job} failed ({Failures} in a row), with {ErrorType}; it runs again at {NextRunUtc:o}. {StackTrace}",
            name, row?.ConsecutiveFailures, errorType, next, failure.StackTrace);

        if (row is { ConsecutiveFailures: FailuresBeforeTelling })
            await TellSetupHoldersAsync(name, row.ConsecutiveFailures);

        return row;
    }

    private async Task<int> RunInScopeAsync(string name, DateTime startedAt, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ScheduledJobScope>().IsJob = true;
        var job = scope.ServiceProvider.GetServices<IScheduledJob>().Single(job => job.Name == name);
        return await job.RunAsync(startedAt, cancellationToken);
    }

    private async Task<ScheduledJob?> CompleteAsync(
        string name,
        ScheduledJobOutcome outcome,
        int? count,
        string? errorType,
        DateTime nextRunUtc,
        CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IScheduledJobRepository>();
        var row = await repository.CompleteAsync(
            name, Holder, outcome, count, errorType, Now(), nextRunUtc, cancellationToken);
        if (row is null)
            logger.LogWarning("Scheduled job {Job}'s run wasn't recorded: its lease had passed to another runner.", name);

        return row;
    }

    /// <summary>
    /// Renews the lease every <see cref="RenewEvery"/> until told to stop. If the lease has passed to someone else (this
    /// runner was too slow to renew it), cancels the run, so two runners never carry on with the same job.
    /// </summary>
    private async Task RenewWhileRunningAsync(string name, CancellationTokenSource run, CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(RenewEvery, timeProvider, stop);
                await using var scope = scopes.CreateAsyncScope();
                var repository = scope.ServiceProvider.GetRequiredService<IScheduledJobRepository>();
                if (await repository.RenewLeaseAsync(name, Holder, Now() + LeaseDuration, stop))
                    continue;

                await run.CancelAsync();
                return;
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                // The next try may work (the database was briefly away); the lease lasts long enough for a few.
                logger.LogWarning("Couldn't renew scheduled job {Job}'s lease: {ErrorType}.", name, ErrorType(exception));
            }
        }
    }

    private static async Task StopRenewingAsync(CancellationTokenSource stopRenewing, Task renewal)
    {
        await stopRenewing.CancelAsync();
        await renewal;
    }

    private async Task ReleaseQuietlyAsync(string name)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IScheduledJobRepository>()
                .ReleaseAsync(name, Holder, CancellationToken.None);
        }
        catch (Exception exception)
        {
            // The lease ends by itself after LeaseDuration.
            logger.LogWarning("Couldn't give up scheduled job {Job}'s lease: {ErrorType}.", name, ErrorType(exception));
        }
    }

    /// <summary>Tells everyone who holds <see cref="Permission.ManageSetup"/> (who sees Scheduled Jobs) that the job keeps failing.</summary>
    private async Task TellSetupHoldersAsync(string name, int failures)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var job = scope.ServiceProvider.GetServices<IScheduledJob>().Single(job => job.Name == name);
            var holders = await scope.ServiceProvider.GetRequiredService<IPermissionHolders>()
                .GetUserIdsAsync(Permission.ManageSetup, CancellationToken.None);
            var sender = scope.ServiceProvider.GetRequiredService<INotificationSender>();
            foreach (var notification in ScheduledJobNotifications.Failing(holders, job.Title, failures, Now()))
                await sender.SendAsync(notification, CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogError("Couldn't tell the ManageSetup holders that scheduled job {Job} keeps failing: {ErrorType}.", name, ErrorType(exception));
        }
    }

    private DateTime Now() => timeProvider.GetUtcNow().UtcDateTime;

    /// <summary>The exception's type name, cut to what the row holds.</summary>
    public static string ErrorType(Exception exception)
    {
        var type = exception.GetType().Name;
        return type.Length <= ScheduledJob.MaxErrorTypeLength ? type : type[..ScheduledJob.MaxErrorTypeLength];
    }

    private static string MakeHolder()
    {
        var holder = $"{Environment.MachineName}/{Environment.ProcessId}/{Guid.NewGuid():N}";
        return holder.Length <= ScheduledJob.MaxLeaseHolderLength ? holder : holder[^ScheduledJob.MaxLeaseHolderLength..];
    }
}
