using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Authentication;
using MedApp.Application.Common.Identity;
using MedApp.Contracts.Authentication.Responses;
using MedApp.Domain.Activity;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Authentication.Users.Commands.ResetUserPassword;

/// <summary>
/// Gives an account a new generated password and ends every session it had, in one transaction with the activity log
/// entry that records the reset (never the password), so none of the three is kept without the others.
/// </summary>
public sealed class ResetUserPasswordHandler(
    IIdentityUserService userService,
    IIdentityReadService readService,
    ISessionService sessionService,
    IIdentityTransactions transactions,
    IActivityLog activityLog)
    : IRequestHandler<ResetUserPasswordCommand, ResetUserPasswordResponse>
{
    public async Task<ResetUserPasswordResponse> Handle(
        ResetUserPasswordCommand request,
        CancellationToken ct)
    {
        ResetUserPasswordResponse? response = null;

        // One transaction: the new password, the end of every session and the log entry are kept together, or none
        // is, so a failure can't leave a password set that the admin was never shown, or a reset nobody recorded. A
        // retry runs it all again.
        await transactions.RunAsync(async () =>
        {
            response = await userService.ResetUserPasswordAsync(
                request.Username,
                ct);

            if (response.GeneratedPassword is null)
                return IdentityResult.Failed([.. response.Errors]);

            // A reset is for a forgotten or exposed password: every session the user had ends, wherever it is, and
            // they sign in again with the new one.
            await sessionService.EndSessionsAsync(readService, request.Username, exceptSessionId: null, ct);

            await activityLog.RecordAsync(
                activityLog.Entry(ActivityAction.PasswordReset, targetUsername: request.Username),
                ct);
            return IdentityResult.Success;
        }, ct);

        return response!;
    }
}
