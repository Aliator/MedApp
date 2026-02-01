using MedApp.Application.Common.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Auth.Commands.CreateUser;

public sealed class CreateUserHandler(
    IIdentityUserService userManagementService)
    : IRequestHandler<CreateUserCommand, IdentityResult>
{
    public async Task<IdentityResult> Handle(
        CreateUserCommand request,
        CancellationToken ct)
    {
        return await userManagementService.CreateUserAsync(
            request.Username,
            request.Password,
            ct);
    }
}