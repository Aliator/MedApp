using MedApp.Application.Common.Identity;
using MedApp.Contracts.Authentication.Responses;
using MediatR;

namespace MedApp.Application.Authentication.Users.Commands.ResetUserPassword;

public sealed class ResetUserPasswordHandler(
    IIdentityUserService userService)
    : IRequestHandler<ResetUserPasswordCommand, ResetUserPasswordResponse>
{
    public async Task<ResetUserPasswordResponse> Handle(
        ResetUserPasswordCommand request,
        CancellationToken ct)
    {
        return await userService.ResetUserPasswordAsync(
            request.Username,
            ct);
    }
}