using MedApp.Application.Activity.Common;
using MedApp.Domain.Activity;

namespace MedApp.Application.Common.Security;

/// <summary>
/// Records security events in the activity log (an <see cref="ActivityEntry"/> each, with the request's address), and
/// in the application log as before (<see cref="LoggingSecurityEventLog"/>). Who it's by:
/// <list type="bullet">
/// <item>an admin's action on someone else (a two-factor reset, an address given or removed): the admin, with the
/// account as its target;</item>
/// <item>a request nobody was signed in for (a reset link asked for, a password reset by link): nobody, from the request's
/// address, with the account as its target, since whoever sent it isn't known to be the account's owner;</item>
/// <item>anything else: the signed-in account itself.</item>
/// </list>
/// The entry's details are the event's small facts (a count, on or off), never an email address, a password, a token
/// or a code.
/// </summary>
public sealed class ActivitySecurityEventLog(
    IActivityLog activityLog,
    LoggingSecurityEventLog applicationLog)
    : ISecurityEventLog
{
    public async Task RecordAsync(SecurityEvent securityEvent, CancellationToken ct)
    {
        await applicationLog.RecordAsync(securityEvent, ct);

        var request = activityLog.Actor;
        var address = request.IpAddress;
        ActivityActor actor;
        string? target;
        if (securityEvent.ActorUserId is { } actorId && actorId != securityEvent.UserId)
        {
            actor = new ActivityActor(actorId, securityEvent.ActorUsername ?? string.Empty, address);
            target = securityEvent.Username;
        }
        else if (request.UserId is null)
        {
            actor = new ActivityActor(null, string.Empty, address);
            target = securityEvent.Username;
        }
        else
        {
            actor = new ActivityActor(securityEvent.UserId, securityEvent.Username ?? string.Empty, address);
            target = null;
        }

        await activityLog.RecordAsync(
            activityLog.Entry(
                ActionFor(securityEvent.Kind),
                details: securityEvent.Details is { Count: > 0 } details ? details : null,
                targetUsername: target,
                actor: actor),
            ct);
    }

    /// <summary>The activity log's action for each kind of security event.</summary>
    public static ActivityAction ActionFor(SecurityEventKind kind) => kind switch
    {
        SecurityEventKind.ResetLinkRequested => ActivityAction.PasswordResetRequested,
        SecurityEventKind.ResetByLink => ActivityAction.PasswordResetByEmail,
        SecurityEventKind.AddressChangeRequested => ActivityAction.EmailChangeRequested,
        SecurityEventKind.AddressConfirmed => ActivityAction.EmailConfirmed,
        SecurityEventKind.AddressRemoved => ActivityAction.EmailRemoved,
        SecurityEventKind.TwoFactorEnabled => ActivityAction.TwoFactorEnabled,
        SecurityEventKind.TwoFactorDisabled => ActivityAction.TwoFactorDisabled,
        SecurityEventKind.TwoFactorReset => ActivityAction.TwoFactorReset,
        SecurityEventKind.RecoveryCodesRegenerated => ActivityAction.RecoveryCodesRegenerated,
        SecurityEventKind.RecoveryCodeUsed => ActivityAction.RecoveryCodeUsed,
        SecurityEventKind.TwoFactorRequirementChanged => ActivityAction.TwoFactorRequirementChanged,
        SecurityEventKind.EmailCodesEnabled => ActivityAction.EmailCodesEnabled,
        SecurityEventKind.EmailCodesDisabled => ActivityAction.EmailCodesDisabled,
        SecurityEventKind.AdminGrantedByCommand => ActivityAction.RolesChanged,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "No activity log action for this kind of security event."),
    };
}
