using MedApp.Application.Jobs.Common;
using MedApp.Domain.Jobs;

namespace MedApp.UnitTests.Common.Constants;

public static class JobsTestConstants
{
    /// <summary>"Now" in job tests: a winter Wednesday, so UK time is UTC.</summary>
    public static readonly DateTime Now = new(2026, 11, 18, 10, 0, 0, DateTimeKind.Utc);

    public const string JobName = "test-job";
    public const string JobTitle = "Test job";
    public const string JobDescription = "Does synthetic work.";

    public static readonly Guid AdminId = Guid.Parse("0000000d-0000-0000-0000-000000000001");
    public static readonly Guid OtherAdminId = Guid.Parse("0000000d-0000-0000-0000-000000000002");

    /// <summary>A row for <see cref="JobName"/>, due at <paramref name="nextRunUtc"/> (default: <see cref="Now"/>).</summary>
    public static ScheduledJob CreateRow(DateTime? nextRunUtc = null) => new()
    {
        Name = JobName,
        NextRunUtc = nextRunUtc ?? Now,
    };

    /// <summary>A job that does what it's told: returns a count, or throws.</summary>
    public sealed class TestJob(JobSchedule? schedule = null) : IScheduledJob
    {
        public string Name { get; init; } = JobName;
        public string Title => JobTitle;
        public string Description => JobDescription;
        public JobSchedule Schedule { get; } = schedule ?? JobSchedule.Every(TimeSpan.FromMinutes(15));

        /// <summary>What a run returns; or, if set, what it throws.</summary>
        public int Count { get; set; } = 7;

        public Exception? Throws { get; set; }

        public List<DateTime> RunsAt { get; } = [];

        public Func<CancellationToken, Task>? During { get; set; }

        public async Task<int> RunAsync(DateTime nowUtc, CancellationToken cancellationToken)
        {
            RunsAt.Add(nowUtc);
            if (During is not null)
                await During(cancellationToken);
            if (Throws is not null)
                throw Throws;
            return Count;
        }
    }
}
