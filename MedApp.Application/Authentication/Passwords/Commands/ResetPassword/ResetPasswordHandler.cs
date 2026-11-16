using MedApp.Application.Common.Authentication;
using MedApp.Application.Common.Email;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Security;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Authentication.Passwords.Commands.ResetPassword;

/// <summary>
/// Resets a forgotten password with an emailed link: the new password (which must meet the rules, avoid the common
/// ones and differ from the current one) and the end of every session the account had are kept together, or neither
/// is. The account's must-change-password flag clears, since its owner chose this one, and its owner is told by email.
/// </summary>
public sealed class ResetPasswordHandler(
    IPasswordResetService resets,
    ISessionService sessionService,
    IIdentityTransactions transactions,
    ISecurityNotices notices,
    ISecurityEventLog events)
    : IRequestHandler<ResetPasswordCommand, IdentityResult>
{
    public async Task<IdentityResult> Handle(ResetPasswordCommand request, CancellationToken ct)
    {
        PasswordResetResult? reset = null;

        var result = await transactions.RunAsync(async () =>
        {
            reset = await resets.ResetAsync(request.UserId, request.Token, request.NewPassword, ct);
            if (!reset.Result.Succeeded)
                return reset.Result;

            // Whoever had the old password (perhaps not the owner) is signed out everywhere.
            await sessionService.RevokeUserSessionsAsync(request.UserId, exceptSessionId: null, ct);
            return IdentityResult.Success;
        }, ct);

        if (!result.Succeeded)
            return result;

        await events.RecordAsync(
            new SecurityEvent(SecurityEventKind.PasswordResetCompleted, request.UserId, reset?.Username),
            ct);
        await notices.SendAsync(request.UserId, SecurityNotice.PasswordReset, ct);

        return result;
    }
}
