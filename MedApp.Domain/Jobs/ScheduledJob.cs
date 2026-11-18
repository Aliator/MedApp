namespace MedApp.Domain.Jobs;

/// <summary>
/// A scheduled job's row: when it's next due, which copy of the API holds its lease (so only one runs it at a time),
/// and how its last run went. One row per job, keyed by the job's name, made the first time a runner sees the job.
/// It holds no personal data: never the exception's message, which can quote data, only its type's name.
/// </summary>
public sealed class ScheduledJob
{
    /// <summary>The longest a job's name can be.</summary>
    public const int MaxNameLength = 100;

    /// <summary>The longest a lease holder's name can be.</summary>
    public const int MaxLeaseHolderLength = 200;

    /// <summary>The longest an error type's name can be.</summary>
    public const int MaxErrorTypeLength = 200;

    /// <summary>The job's code name, e.g. "operational-purge" (the key).</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>When it's next due.</summary>
    public DateTime NextRunUtc { get; set; }

    /// <summary>The runner (a copy of the API, or a command) holding its lease, while it runs.</summary>
    public string? LeaseHolder { get; set; }

    /// <summary>When the lease ends unless it's renewed: after it, another runner may take the job.</summary>
    public DateTime? LeaseUntilUtc { get; set; }

    public DateTime? LastStartedAtUtc { get; set; }
    public DateTime? LastFinishedAtUtc { get; set; }

    /// <summary>How the last run ended; null before its first.</summary>
    public ScheduledJobOutcome? LastOutcome { get; set; }

    /// <summary>How many items the last successful run handled (for the purge, rows deleted, or would delete).</summary>
    public int? LastCount { get; set; }

    /// <summary>Failed runs in a row; a success sets it back to 0.</summary>
    public int ConsecutiveFailures { get; set; }

    /// <summary>The last failure's exception type name (never its message).</summary>
    public string? LastErrorType { get; set; }
}
