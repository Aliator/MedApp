namespace MedApp.Domain.Jobs;

/// <summary>How a scheduled job's last run ended. Stored as a number, so values never change.</summary>
public enum ScheduledJobOutcome
{
    Succeeded = 0,
    Failed = 1,

    /// <summary>It was due but switched off (<c>Jobs:{Name}:Enabled</c>), so it did nothing.</summary>
    Skipped = 2
}
