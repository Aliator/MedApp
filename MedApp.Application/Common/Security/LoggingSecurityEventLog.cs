using Microsoft.Extensions.Logging;

namespace MedApp.Application.Common.Security;

/// <summary>
/// Writes security events to the application log, at Information, as structured fields: the kind, the account's ID
/// and username, who did it, and the details. Never an email address, password, token or code (events don't carry
/// them).
/// </summary>
public sealed class LoggingSecurityEventLog(ILogger<LoggingSecurityEventLog> logger) : ISecurityEventLog
{
    public Task RecordAsync(SecurityEvent securityEvent, CancellationToken ct)
    {
        logger.LogInformation(
            "Security event {SecurityEventKind} for user {UserId} ({Username}) by {ActorUserId} ({ActorUsername}): {Details}",
            securityEvent.Kind,
            securityEvent.UserId,
            securityEvent.Username,
            securityEvent.ActorUserId ?? securityEvent.UserId,
            securityEvent.ActorUsername ?? securityEvent.Username,
            Describe(securityEvent.Details));

        return Task.CompletedTask;
    }

    /// <summary>The details as <c>key=value</c> pairs, or "none".</summary>
    public static string Describe(IReadOnlyDictionary<string, object?>? details) =>
        details is { Count: > 0 }
            ? string.Join(", ", details.Select(pair => $"{pair.Key}={Value(pair.Value)}"))
            : "none";

    /// <summary>A value as text; a list (a role change's permissions added) as its items, not its type's name.</summary>
    private static string? Value(object? value) =>
        value is System.Collections.IEnumerable list and not string
            ? "[" + string.Join(", ", list.Cast<object?>()) + "]"
            : value?.ToString();
}
