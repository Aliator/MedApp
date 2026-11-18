using MedApp.Application.Pathways.Common;

namespace MedApp.Application.Monitoring.Common;

/// <summary>
/// Tells admins about a saved change to a pathway that matches their monitoring rules. Every pathway command
/// calls it after saving its <see cref="PathwayChange"/>, as it sends any other notification. Best effort: it never
/// throws (except when cancelled), because the change is already saved.
/// </summary>
public interface IPathwayMonitor
{
    Task NotifyAsync(PathwayChange change, CancellationToken cancellationToken);
}
