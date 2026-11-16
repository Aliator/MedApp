namespace MedApp.Application.Common.Security;

/// <summary>
/// Records security events (two-factor changes, password resets and their requests, recovery codes used). A seam:
/// today it writes to the application log (<see cref="LoggingSecurityEventLog"/>); once the activity log exists, an
/// implementation that writes there replaces it, and the handlers don't change. Recording never throws for a failure
/// to record.
/// </summary>
public interface ISecurityEventLog
{
    Task RecordAsync(SecurityEvent securityEvent, CancellationToken ct);
}
