using MedApp.Application.Common.Identity;
using MediatR;

namespace MedApp.Application.Authentication.Users.Commands.CreateUser;

public sealed class CreateUserHandler(
    IIdentityUserService userManagementService)
    : IRequestHandler<CreateUserCommand, CreateUserResult>
{
    public async Task<CreateUserResult> Handle(
        CreateUserCommand request,
        CancellationToken ct)
    {
        return await userManagementService.CreateUserAsync(
            request.Username,
            ct);
    }
}