namespace MedApp.Application.Jobs.Common;

/// <summary>
/// Whether the current scope is a scheduled job's run (the runner marks the scope it makes for each run). The host uses
/// it to give jobs the system as their actor (<see cref="SystemActivityContext"/>) instead of a request's user.
/// </summary>
public sealed class ScheduledJobScope
{
    public bool IsJob { get; set; }
}
