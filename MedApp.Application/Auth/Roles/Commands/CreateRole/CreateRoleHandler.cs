using MedApp.Application.Common.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Auth.Roles.Commands.CreateRole;

public sealed class CreateRoleHandler(
    IIdentityRoleService roleService)
    : IRequestHandler<CreateRoleCommand, IdentityRole<Guid>?>
{
    public async Task<IdentityRole<Guid>?> Handle(
        CreateRoleCommand request,
        CancellationToken ct)
    {
        return await roleService.CreateRoleAsync(request.Name, ct);
    }
}