using MedApp.Application.Common.Identity;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Infrastructure.Identity;

public sealed class IdentityReadService(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager)
    : IIdentityReadService
{
    public Task<IReadOnlyList<string>> GetUsernamesAsync(CancellationToken ct)
    {
        return Task.FromResult<IReadOnlyList<string>>(userManager.Users
            .Select(u => u.UserName!)
            .ToList());
    }

    public Task<IReadOnlyList<string>> GetRoleNamesAsync(CancellationToken ct)
    {
        return Task.FromResult<IReadOnlyList<string>>(roleManager.Roles
            .Select(r => r.Name!)
            .ToList());
    }

    public async Task<IReadOnlyList<string>> GetRolesForUserAsync(
        string username,
        CancellationToken ct)
    {
        var user = await userManager.FindByNameAsync(username);
        if (user is null)
            return [];

        var roles = await userManager.GetRolesAsync(user);
        return roles.ToList();
    }
}