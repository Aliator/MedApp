using MedApp.Application.Procedures.Common;

namespace MedApp.Application.Monitoring.Common;

/// <summary>
/// Tells admins about a saved change to a procedure that matches their monitoring rules. Every procedure command
/// calls it after saving its <see cref="ProcedureChange"/>, as it sends any other notification. Best effort: it never
/// throws (except when cancelled), because the change is already saved.
/// </summary>
public interface IProcedureMonitor
{
    Task NotifyAsync(ProcedureChange change, CancellationToken cancellationToken);
}
