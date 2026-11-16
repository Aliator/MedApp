namespace MedApp.Application.Common.Security;

/// <summary>
/// Records security events (two-factor changes, password resets and their requests, email address changes, recovery
/// codes used): in the activity log and the application log (<see cref="ActivitySecurityEventLog"/>). Handlers record
/// after their own change is saved, as for the activity log's other account entries.
/// </summary>
public interface ISecurityEventLog
{
    Task RecordAsync(SecurityEvent securityEvent, CancellationToken ct);
}
