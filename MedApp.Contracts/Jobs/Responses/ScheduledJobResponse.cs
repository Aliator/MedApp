namespace MedApp.Contracts.Jobs.Responses;

/// <summary>A scheduled job: what it does, when it runs, and how its last run went.</summary>
public sealed class ScheduledJobResponse
{
    /// <summary>Its code name, e.g. "operational-purge" (what Run now takes).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Its name for people, e.g. "Operational data purge".</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>What it does.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>When it runs, in words: "Daily at 04:30 (UK time)", "Every 15 minutes".</summary>
    public string Schedule { get; set; } = string.Empty;

    /// <summary>Whether it's switched on (<c>Jobs:{Name}:Enabled</c>): when off, it's skipped, and can't be run now.</summary>
    public bool Enabled { get; set; }

    /// <summary>Whether a copy of the API is running it now (holds its lease).</summary>
    public bool IsRunning { get; set; }

    /// <summary>Whether it's switched on, not running, and more than two intervals past its next run (as <c>/health</c> reports).</summary>
    public bool IsOverdue { get; set; }

    /// <summary>When it's next due; null before any runner has seen it.</summary>
    public DateTime? NextRunUtc { get; set; }

    public DateTime? LastStartedAtUtc { get; set; }

    public DateTime? LastFinishedAtUtc { get; set; }

    /// <summary>How its last run ended, by name: "Succeeded", "Failed" or "Skipped"; null before its first.</summary>
    public string? LastOutcome { get; set; }

    /// <summary>How many items its last successful run handled (for the purge, rows deleted, or in a dry run would be).</summary>
    public int? LastCount { get; set; }

    /// <summary>Failed runs in a row.</summary>
    public int ConsecutiveFailures { get; set; }

    /// <summary>The last failure's exception type, e.g. "SqlException" (never its message).</summary>
    public string? LastErrorType { get; set; }
}
