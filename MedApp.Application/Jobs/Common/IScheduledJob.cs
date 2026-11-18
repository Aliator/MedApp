namespace MedApp.Application.Jobs.Common;

/// <summary>
/// A job the API runs on a schedule (<see cref="ScheduledJobRunner"/>): registered as a scoped service, so each run
/// gets a scope of its own. Exactly one runner runs it at a time, across every copy of the API (a lease in SQL Server).
/// A job must be idempotent: each item it handles is marked done (or deleted) in the same save as the work, so a run
/// cut short and run again repeats nothing. It logs counts, never patient details.
/// </summary>
public interface IScheduledJob
{
    /// <summary>Its code name (lower-case words with hyphens, e.g. "operational-purge"): the row's key and the setting's.</summary>
    string Name { get; }

    /// <summary>Its name for people, e.g. "Operational data purge".</summary>
    string Title { get; }

    /// <summary>What it does, in a sentence or two, for the Scheduled Jobs page.</summary>
    string Description { get; }

    JobSchedule Schedule { get; }

    /// <summary>Does the work as at <paramref name="nowUtc"/>, and returns how many items it handled.</summary>
    Task<int> RunAsync(DateTime nowUtc, CancellationToken cancellationToken);
}
