using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Authentication;
using MedApp.Application.Common.Identity;
using MedApp.Domain.Activity;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Authentication.Users.Commands.UpdateUserPassword;

/// <summary>
/// Changes a user's own password and ends their other sessions, in one transaction with the activity log entry that
/// records the change.
/// </summary>
public sealed class UpdateUserPasswordHandler(
    IIdentityUserService userService,
    IIdentityReadService readService,
    ISessionService sessionService,
    IIdentityTransactions transactions,
    IActivityLog activityLog)
    : IRequestHandler<UpdateUserPasswordCommand, IdentityResult>
{
    public async Task<IdentityResult> Handle(
        UpdateUserPasswordCommand request,
        CancellationToken ct)
    {
        // One transaction: the new password, the end of the other sessions and the log entry are kept together, or
        // none is.
        return await transactions.RunAsync(async () =>
        {
            var result = await userService.UpdateUserPasswordAsync(
                request.Username,
                request.OldPassword,
                request.NewPassword,
                ct);

            if (result.Succeeded)
            {
                // Anyone signed in elsewhere with the old password (perhaps not the user) is signed out; this session
                // stays.
                await sessionService.EndSessionsAsync(readService, request.Username, request.CurrentSessionId, ct);

                await activityLog.RecordAsync(
                    activityLog.Entry(ActivityAction.PasswordChanged, targetUsername: request.Username),
                    ct);
            }

            return result;
        }, ct);
    }
}
