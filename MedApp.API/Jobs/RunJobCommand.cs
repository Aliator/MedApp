using MedApp.Application.Common.Exceptions;
using MedApp.Application.Jobs.Common;
using MedApp.Domain.Jobs;

namespace MedApp.API.Jobs;

/// <summary>
/// The API's <c>run-job &lt;name&gt;</c> command: runs one scheduled job now, through the runner (so it takes the job's
/// lease like any run), then exits without starting the web server. For running a job by hand, e.g. in a pod with
/// <c>kubectl exec</c>.
/// </summary>
public static class RunJobCommand
{
    public const string Command = "run-job";

    /// <summary>Runs the job; the process's exit code: 0 when it succeeded, 1 when it failed, was refused or is unknown.</summary>
    public static async Task<int> RunAsync(IServiceProvider services, string name, CancellationToken ct)
    {
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(RunJobCommand));
        try
        {
            var row = await services.GetRequiredService<IScheduledJobRunner>().RunNowAsync(name, ct);
            logger.LogInformation(
                "Scheduled job {Job}: {Outcome}, {Count} items.", name, row.LastOutcome, row.LastCount);
            return row.LastOutcome == ScheduledJobOutcome.Succeeded ? 0 : 1;
        }
        catch (Exception exception) when (exception is NotFoundException or ConflictException)
        {
            logger.LogError("Scheduled job {Job} not run: {Reason}", name, exception.Message);
            return 1;
        }
    }
}
