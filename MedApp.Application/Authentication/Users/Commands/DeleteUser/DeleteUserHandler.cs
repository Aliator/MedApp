using MedApp.Application.Common.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Authentication.Users.Commands.DeleteUser;

public sealed class DeleteUserHandler(
    IIdentityUserService userService)
    : IRequestHandler<DeleteUserCommand, IdentityResult>
{
    public async Task<IdentityResult> Handle(
        DeleteUserCommand request,
        CancellationToken ct)
    {
        // Deleting an account can't be undone, so the admin confirms with their own password first.
        var confirmed = await userService.VerifyPasswordAsync(request.ActorUsername, request.ActorPassword, ct);
        if (!confirmed.Succeeded)
            return confirmed;

        return await userService.DeleteUserAsync(
            request.Username,
            ct);
    }
}