using MedApp.Contracts.Jobs.Responses;
using MedApp.Domain.Jobs;

namespace MedApp.Application.Jobs.Common;

public static class ScheduledJobMapper
{
    /// <param name="job">The job.</param>
    /// <param name="row">The job's row; null before any runner has seen it.</param>
    /// <param name="enabled">Whether it's switched on (<see cref="JobsOptions.IsJobEnabled"/>).</param>
    /// <param name="nowUtc">Now, for whether it's running or overdue.</param>
    public static ScheduledJobResponse ToResponse(IScheduledJob job, ScheduledJob? row, bool enabled, DateTime nowUtc) => new()
    {
        Name = job.Name,
        Title = job.Title,
        Description = job.Description,
        Schedule = job.Schedule.Describe(),
        Enabled = enabled,
        IsRunning = IsRunning(row, nowUtc),
        IsOverdue = IsOverdue(job.Schedule, row, enabled, nowUtc),
        NextRunUtc = row?.NextRunUtc,
        LastStartedAtUtc = row?.LastStartedAtUtc,
        LastFinishedAtUtc = row?.LastFinishedAtUtc,
        LastOutcome = row?.LastOutcome?.ToString(),
        LastCount = row?.LastCount,
        ConsecutiveFailures = row?.ConsecutiveFailures ?? 0,
        LastErrorType = row?.LastErrorType
    };

    /// <summary>Whether a runner holds the job's lease now.</summary>
    public static bool IsRunning(ScheduledJob? row, DateTime nowUtc) => row?.LeaseUntilUtc > nowUtc;

    /// <summary>
    /// Whether a job has stopped running: switched on, not running now, and more than two intervals past its next run.
    /// <c>/health</c> reports Degraded while any job is.
    /// </summary>
    public static bool IsOverdue(JobSchedule schedule, ScheduledJob? row, bool enabled, DateTime nowUtc) =>
        enabled && row is not null && !IsRunning(row, nowUtc) && schedule.IsOverdue(row.NextRunUtc, nowUtc);
}
