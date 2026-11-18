using MedApp.Application.Jobs.Common;
using Microsoft.Extensions.Options;

namespace MedApp.API.Jobs;

/// <summary>
/// Runs the scheduled jobs on a timer: every <see cref="PollInterval"/>, each job that's due and whose lease this copy
/// can take (<see cref="IScheduledJobRunner.RunDueAsync"/>). Off when <c>Jobs:Enabled</c> is false (the integration
/// tests run jobs themselves). A failed round (the database away, say, or not migrated yet) is logged and tried again
/// at the next tick; the API keeps serving.
/// </summary>
public sealed class ScheduledJobHost(
    IScheduledJobRunner runner,
    IOptions<JobsOptions> options,
    TimeProvider timeProvider,
    ILogger<ScheduledJobHost> logger) : BackgroundService
{
    public static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

    /// <summary>Failed rounds in a row between warnings: one at first, then every 10 minutes while it lasts.</summary>
    private const int WarnEvery = 20;

    private int _failedRounds;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("Scheduled jobs don't run on this copy of the API (Jobs:Enabled is false).");
            return;
        }

        using var timer = new PeriodicTimer(PollInterval, timeProvider);
        do
        {
            await RunDueAsync(stoppingToken);
        }
        while (await WaitAsync(timer, stoppingToken));
    }

    /// <summary>One round: every due job. Never throws, but when the API is stopping.</summary>
    public async Task RunDueAsync(CancellationToken stoppingToken)
    {
        try
        {
            await runner.RunDueAsync(stoppingToken);
            _failedRounds = 0;
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            // The type only: a message can quote data. Warned once, then now and then, so a database that isn't
            // migrated yet doesn't fill the log.
            var level = _failedRounds++ % WarnEvery == 0 ? LogLevel.Warning : LogLevel.Debug;
            logger.Log(
                level,
                "Couldn't run the scheduled jobs ({FailedRounds} rounds in a row), with {ErrorType}; trying again in {Seconds} s.",
                _failedRounds, exception.GetType().Name, PollInterval.TotalSeconds);
        }
    }

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
