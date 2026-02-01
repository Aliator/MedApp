using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Common.Identity;

public sealed class IdentityUserRoleService(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager)
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
        if (role is null)
            return null;

        var result = await userManager.AddToRoleAsync(user, role.Name);
        
        return !result.Succeeded ? null : role;
    }
}