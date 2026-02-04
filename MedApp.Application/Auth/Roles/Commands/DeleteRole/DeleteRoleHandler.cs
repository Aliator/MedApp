using MedApp.Application.Common.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Auth.Roles.Commands.DeleteRole;

public sealed class DeleteRoleHandler(
    IIdentityRoleService roleService)
    : IRequestHandler<DeleteRoleCommand, IdentityResult>
{
    public async Task<IdentityResult> Handle(
        DeleteRoleCommand request,
        CancellationToken ct)
    {
        return await roleService.DeleteRoleAsync(
            request.RoleName,
            ct);
    }
}