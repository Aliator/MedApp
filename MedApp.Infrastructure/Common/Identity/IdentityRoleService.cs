using MedApp.Application.Common.Identity;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Infrastructure.Common.Identity;

public sealed class IdentityRoleService(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager) : IIdentityRoleService
{
    public async Task<IdentityRole<Guid>?> AssignRoleAsync(
        string username,
        string roleName,
        CancellationToken ct)
    {
        var user = await userManager.FindByNameAsync(username);
        if (user is null)
            return null;

        var role = await roleManager.FindByNameAsync(roleName);
        if (role?.Name is null)
            return null;

        var result = await userManager.AddToRoleAsync(user, role.Name);
        
        return !result.Succeeded ? null : role;
    }
    
    public async Task<IdentityResult> DeleteRoleAsync(
        string roleName,
        CancellationToken ct)
    {
        var role = await roleManager.FindByNameAsync(roleName);
        if (role is null)
            return IdentityResult.Failed(new IdentityError
            {
                Code = "RoleNotFound",
                Description = "Role not found."
            });

        return await roleManager.DeleteAsync(role);
    }
}