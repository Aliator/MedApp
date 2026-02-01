using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Infrastructure.Common.Identity;

public sealed class IdentityUserRoleService(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager)
    : IUserRoleService
{
    public async Task AssignRoleAsync(
        string username,
        string role,
        CancellationToken ct)
    {
        var user = await userManager.FindByNameAsync(username);
        if (user is null)
            throw new IdentityOperationException("User not found.");

        if (!await roleManager.RoleExistsAsync(role))
            throw new IdentityOperationException("Role not found.");

        await userManager.AddToRoleAsync(user, role);
    }
}