using MedApp.Application.Common.Authentication;
using MedApp.Application.Common.Email;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Security;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Authentication.TwoFactor.Commands.ResetUserTwoFactor;

/// <summary>
/// Turns the user's two-factor off (forgetting their key and recovery codes) and ends every session they have,
/// together. It's recorded with the admin who did it, and the user is told by email. Fails with
/// <see cref="IdentityErrors.UserNotFound"/> or, for the admin's own account, <see cref="IdentityErrors.OwnTwoFactor"/>.
/// If admins must use two-factor, an admin reset this way is asked to set it up again at their next sign-in.
/// </summary>
public sealed class ResetUserTwoFactorHandler(
    IIdentityReadService readService,
    ITwoFactorService twoFactor,
    ISessionService sessionService,
    IIdentityTransactions transactions,
    ISecurityNotices notices,
    ISecurityEventLog events)
    : IRequestHandler<ResetUserTwoFactorCommand, IdentityResult>
{
    public async Task<IdentityResult> Handle(ResetUserTwoFactorCommand request, CancellationToken ct)
    {
        var user = await readService.GetUserAsync(request.Username, ct);
        if (user is null)
            return IdentityResult.Failed(IdentityErrors.UserNotFound);

        // A reset is for someone else who is locked out; an admin turns off their own with their password and a code.
        if (user.UserId == request.ActorUserId)
            return IdentityResult.Failed(IdentityErrors.OwnTwoFactor);

        var result = await transactions.RunAsync(async () =>
        {
            var disabled = await twoFactor.DisableAsync(user.UserId, ct);
            if (!disabled.Succeeded)
                return disabled;

            await sessionService.RevokeUserSessionsAsync(user.UserId, exceptSessionId: null, ct);
            return IdentityResult.Success;
        }, ct);

        if (!result.Succeeded)
            return result;

        await events.RecordAsync(
            new SecurityEvent(SecurityEventKind.TwoFactorReset, user.UserId, user.Username, request.ActorUserId, request.ActorUsername),
            ct);
        await notices.SendAsync(user.UserId, SecurityNotice.TwoFactorReset, ct);

        return result;
    }
}
