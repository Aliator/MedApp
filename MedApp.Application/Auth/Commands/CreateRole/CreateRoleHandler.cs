using MediatR;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Auth.Commands.CreateRole;

public sealed class CreateRoleHandler(
    RoleManager<IdentityRole<Guid>> roleManager)
    : IRequestHandler<CreateRoleCommand>
{
    public async Task Handle(CreateRoleCommand request, CancellationToken ct)
    {
        if (await roleManager.RoleExistsAsync(request.Name))
            return;

        var result = await roleManager.CreateAsync(
            new IdentityRole<Guid>(request.Name));

        if (!result.Succeeded)
            throw new InvalidOperationException("Failed to create role.");
    }
}
