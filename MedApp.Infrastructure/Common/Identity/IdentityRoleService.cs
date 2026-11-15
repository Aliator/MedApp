using MedApp.Application.Common.Identity;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Infrastructure.Common.Identity;

public sealed class IdentityRoleService(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    IIdentityTransactions transactions) : IIdentityRoleService
{
    public async Task<IdentityRole<Guid>?> CreateRoleAsync(
        string roleName,
        CancellationToken ct)
    {
        var existingRole = await roleManager.FindByNameAsync(roleName);
        if (existingRole is not null)
            return existingRole;

        var role = new IdentityRole<Guid>(roleName);
        var result = await roleManager.CreateAsync(role);

        return result.Succeeded ? role : null;
    }

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

    public async Task<IdentityRole<Guid>?> RevokeRoleAsync(
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

        var result = await userManager.RemoveFromRoleAsync(user, role.Name);

        return !result.Succeeded ? null : role;
    }

    public async Task<IdentityResult> SetRolesAsync(
        string username,
        IReadOnlyCollection<string> roleNames,
        CancellationToken ct)
    {
        var user = await userManager.FindByNameAsync(username);
        if (user is null)
            return IdentityResult.Failed(IdentityErrors.UserNotFound);

        // Every role must exist; their stored names are the ones Identity matches on.
        var wanted = new List<string>();
        foreach (var roleName in roleNames.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var role = await roleManager.FindByNameAsync(roleName);
            if (role?.Name is null)
                return IdentityResult.Failed(IdentityErrors.RoleNotFound);

            wanted.Add(role.Name);
        }

        var current = await userManager.GetRolesAsync(user);
        var toRemove = current.Except(wanted, StringComparer.OrdinalIgnoreCase).ToList();
        var toAdd = wanted.Except(current, StringComparer.OrdinalIgnoreCase).ToList();

        if (toRemove.Count == 0 && toAdd.Count == 0)
            return IdentityResult.Success;

        return await transactions.RunAsync(async () =>
        {
            if (toRemove.Count > 0)
            {
                var removed = await userManager.RemoveFromRolesAsync(user, toRemove);
                if (!removed.Succeeded)
                    return removed;
            }

            return toAdd.Count > 0
                ? await userManager.AddToRolesAsync(user, toAdd)
                : IdentityResult.Success;
        }, ct);
    }

    public async Task<IdentityResult> DeleteRoleAsync(
        string roleName,
        CancellationToken ct)
    {
        var role = await roleManager.FindByNameAsync(roleName);
        if (role is null)
            return IdentityResult.Failed(IdentityErrors.RoleNotFound);

        return await roleManager.DeleteAsync(role);
    }
}