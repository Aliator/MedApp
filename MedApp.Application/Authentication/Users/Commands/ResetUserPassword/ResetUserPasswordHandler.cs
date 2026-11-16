using MedApp.Application.Common.Authentication;
using MedApp.Application.Common.Identity;
using MedApp.Contracts.Authentication.Responses;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Authentication.Users.Commands.ResetUserPassword;

public sealed class ResetUserPasswordHandler(
    IIdentityUserService userService,
    IIdentityReadService readService,
    ISessionService sessionService,
    IIdentityTransactions transactions)
    : IRequestHandler<ResetUserPasswordCommand, ResetUserPasswordResponse>
{
    public async Task<ResetUserPasswordResponse> Handle(
        ResetUserPasswordCommand request,
        CancellationToken ct)
    {
        ResetUserPasswordResponse? response = null;

        // One transaction: the new password and the end of every session are kept together, or neither is, so a
        // failure can't leave a password set that the admin was never shown. A retry runs it all again.
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
            return IdentityResult.Success;
        }, ct);

        return response!;
    }
}
