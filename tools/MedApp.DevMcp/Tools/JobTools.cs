using System.ComponentModel;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Jobs.Common;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace MedApp.DevMcp.Tools;

[McpServerToolType]
public sealed class JobTools
{
    [McpServerTool(Name = "run_job")]
    [Description(
        "Runs one of the API's scheduled jobs now against the local database, through the same runner and lease as the API " +
        "(refused while a running API copy holds it), and returns how it went. Jobs: operational-purge (a dry run unless " +
        "Retention:Operational:DryRun is false in devmcp.json: it then deletes old sessions, sign-in events, notifications, " +
        "notepads and expired codes).")]
    public static async Task<object> RunJob(
        IScheduledJobRunner runner,
        [Description("The job's name, e.g. operational-purge.")] string name,
        CancellationToken ct = default)
    {
        try
        {
            var row = await runner.RunNowAsync(name, ct);
            return new
            {
                name = row.Name,
                outcome = row.LastOutcome?.ToString(),
                count = row.LastCount,
                consecutiveFailures = row.ConsecutiveFailures,
                errorType = row.LastErrorType,
                startedAtUtc = row.LastStartedAtUtc,
                finishedAtUtc = row.LastFinishedAtUtc,
                nextRunUtc = row.NextRunUtc,
            };
        }
        catch (Exception exception) when (exception is NotFoundException or ConflictException)
        {
            throw new McpException(exception.Message);
        }
    }
}
