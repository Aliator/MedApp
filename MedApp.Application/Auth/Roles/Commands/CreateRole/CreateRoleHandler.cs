using MediatR;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Auth.Roles.Commands.CreateRole;

public sealed class CreateRoleHandler(
    RoleManager<IdentityRole<Guid>> roleManager)
    : IRequestHandler<CreateRoleCommand, IdentityRole<Guid>?>
{
    public async Task<IdentityRole<Guid>?> Handle(
        CreateRoleCommand request,
        CancellationToken ct)
    {
        var existingRole = await roleManager.FindByNameAsync(request.Name);
        if (existingRole is not null)
            return existingRole;

        var role = new IdentityRole<Guid>(request.Name);
        var result = await roleManager.CreateAsync(role);

        return result.Succeeded ? role : null;
    }
}