using MedApp.Application.Common.Authentication;
using MedApp.Application.Common.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Authentication.Users.Commands.UpdateUserPassword;

public sealed class UpdateUserPasswordHandler(
    IIdentityUserService userService,
    IIdentityReadService readService,
    ISessionService sessionService,
    IIdentityTransactions transactions)
    : IRequestHandler<UpdateUserPasswordCommand, IdentityResult>
{
    public async Task<IdentityResult> Handle(
        UpdateUserPasswordCommand request,
        CancellationToken ct)
    {
        // One transaction: the new password and the end of the other sessions are kept together, or neither is.
        return await transactions.RunAsync(async () =>
        {
            var result = await userService.UpdateUserPasswordAsync(
                request.Username,
                request.OldPassword,
                request.NewPassword,
                ct);

            // Anyone signed in elsewhere with the old password (perhaps not the user) is signed out; this session
            // stays.
            if (result.Succeeded)
                await sessionService.EndSessionsAsync(readService, request.Username, request.CurrentSessionId, ct);

            return result;
        }, ct);
    }
}
