using MedApp.Application.Activity.Common;
using MedApp.Domain.Activity;

namespace MedApp.Application.Common.Security;

/// <summary>
/// Records security events in the activity log (an <see cref="ActivityEntry"/> each, with the request's address), and
/// in the application log as before (<see cref="LoggingSecurityEventLog"/>). An event about someone's own account is
/// by them; one an admin made (a two-factor reset) is by the admin, with the account as its target. The entry's details
/// are the event's small facts (a count, on or off), never an email address, a password, a token or a code.
/// </summary>
public sealed class ActivitySecurityEventLog(
    IActivityLog activityLog,
    LoggingSecurityEventLog applicationLog)
    : ISecurityEventLog
{
    public async Task RecordAsync(SecurityEvent securityEvent, CancellationToken ct)
    {
        await applicationLog.RecordAsync(securityEvent, ct);

        var address = activityLog.Actor.IpAddress;
        var byAnother = securityEvent.ActorUserId is { } actorId && actorId != securityEvent.UserId;
        var actor = byAnother
            ? new ActivityActor(securityEvent.ActorUserId, securityEvent.ActorUsername ?? string.Empty, address)
            : new ActivityActor(securityEvent.UserId, securityEvent.Username ?? string.Empty, address);

        await activityLog.RecordAsync(
            activityLog.Entry(
                ActionFor(securityEvent.Kind),
                details: securityEvent.Details is { Count: > 0 } details ? details : null,
                targetUsername: byAnother ? securityEvent.Username : null,
                actor: actor),
            ct);
    }

    /// <summary>The activity log's action for each kind of security event.</summary>
    public static ActivityAction ActionFor(SecurityEventKind kind) => kind switch
    {
        SecurityEventKind.PasswordResetRequested => ActivityAction.PasswordResetRequested,
        SecurityEventKind.PasswordResetCompleted => ActivityAction.PasswordResetByEmail,
        SecurityEventKind.EmailChangeRequested => ActivityAction.EmailChangeRequested,
        SecurityEventKind.EmailConfirmed => ActivityAction.EmailConfirmed,
        SecurityEventKind.EmailRemoved => ActivityAction.EmailRemoved,
        SecurityEventKind.TwoFactorEnabled => ActivityAction.TwoFactorEnabled,
        SecurityEventKind.TwoFactorDisabled => ActivityAction.TwoFactorDisabled,
        SecurityEventKind.TwoFactorReset => ActivityAction.TwoFactorReset,
        SecurityEventKind.RecoveryCodesRegenerated => ActivityAction.RecoveryCodesRegenerated,
        SecurityEventKind.RecoveryCodeUsed => ActivityAction.RecoveryCodeUsed,
        SecurityEventKind.TwoFactorRequirementChanged => ActivityAction.TwoFactorRequirementChanged,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "No activity log action for this kind of security event."),
    };
}
