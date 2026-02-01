using MediatR;
using MedApp.Application.Common.Identity;

namespace MedApp.Application.Auth.Commands.AssignRole;

public sealed class AssignRoleHandler(
    IUserRoleService userRoleService)
    : IRequestHandler<AssignRoleCommand>
{
    public async Task Handle(AssignRoleCommand request, CancellationToken ct)
    {
        await userRoleService.AssignRoleAsync(
            request.Username,
            request.Role,
            ct);
    }
}