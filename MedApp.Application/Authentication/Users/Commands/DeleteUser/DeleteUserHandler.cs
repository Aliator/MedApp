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
        return await userService.DeleteUserAsync(
            request.Username,
            ct);
    }
}