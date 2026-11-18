using MedApp.Application.Jobs.Common;
using MedApp.Application.Jobs.Repositories;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace MedApp.API.Jobs;

/// <summary>
/// <c>/health</c>'s check of the scheduled jobs: Degraded while any job is overdue (switched on, not running, and more
/// than two intervals past its next run: no copy is running jobs), or when the jobs can't be read. Never Unhealthy:
/// Kubernetes' readiness probe reads <c>/health</c>, and a late job is no reason to stop serving (Degraded is still a
/// 200).
/// </summary>
public sealed class ScheduledJobsHealthCheck(
    IServiceScopeFactory scopes,
    IOptions<JobsOptions> options,
    TimeProvider timeProvider) : IHealthCheck
{
    public const string Name = "scheduled-jobs";

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var rows = (await scope.ServiceProvider.GetRequiredService<IScheduledJobRepository>().GetAllAsync(cancellationToken))
                .ToDictionary(row => row.Name, StringComparer.Ordinal);
            var now = timeProvider.GetUtcNow().UtcDateTime;

            var overdue = scope.ServiceProvider.GetServices<IScheduledJob>()
                .Where(job => ScheduledJobMapper.IsOverdue(
                    job.Schedule, rows.GetValueOrDefault(job.Name), options.Value.IsJobEnabled(job.Name), now))
                .Select(job => job.Name)
                .ToList();

            return overdue.Count == 0
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Degraded($"Overdue scheduled jobs: {string.Join(", ", overdue)}.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Degraded($"Couldn't read the scheduled jobs ({exception.GetType().Name}).");
        }
    }
}
