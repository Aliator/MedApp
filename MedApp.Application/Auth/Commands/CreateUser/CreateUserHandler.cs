using MedApp.Application.Common.Identity;
using MediatR;

namespace MedApp.Application.Auth.Commands.CreateUser;

public sealed class CreateUserHandler(
    IUserManagementService userManagementService)
    : IRequestHandler<CreateUserCommand, Guid>
{
    public async Task<Guid> Handle(CreateUserCommand request, CancellationToken ct)
    {
        return await userManagementService.CreateUserAsync(
            request.Username,
            request.Password,
            ct);
    }
}
