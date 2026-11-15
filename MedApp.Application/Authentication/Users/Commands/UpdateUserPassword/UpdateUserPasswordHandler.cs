using MedApp.Application.Common.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Authentication.Users.Commands.UpdateUserPassword;

public sealed class UpdateUserPasswordHandler(
    IIdentityUserService userService)
    : IRequestHandler<UpdateUserPasswordCommand, IdentityResult>
{
    public async Task<IdentityResult> Handle(
        UpdateUserPasswordCommand request,
        CancellationToken ct)
    {
        return await userService.UpdateUserPasswordAsync(
            request.Username,
            request.OldPassword,
            request.NewPassword,
            ct);
    }
}