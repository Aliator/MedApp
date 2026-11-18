using MedApp.Application.Common.Identity;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Infrastructure.Common.Identity;

public sealed class IdentityRoleService(
    UserManager<ApplicationUser> userManager,
    RoleManager<ApplicationRole> roleManager,
    IIdentityTransactions transactions) : IIdentityRoleService
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
        // Everything is loaded inside the work, which a retry runs again from the start (IdentityTransactions): a user
        // loaded once, outside it, would carry the failed attempt's concurrency stamp into the retry and fail it.
        return await transactions.RunAsync(async () =>
        {
            var user = await userManager.FindByNameAsync(username);
            if (user is null)
                return IdentityResult.Failed(IdentityErrors.UserNotFound);

            // Every role must exist; their stored names are the ones Identity matches on.
            var wanted = new List<string>();
            var archived = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var roleName in roleNames.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var role = await roleManager.FindByNameAsync(roleName);
                if (role?.Name is null)
                    return IdentityResult.Failed(IdentityErrors.RoleNotFound);

                wanted.Add(role.Name);
                if (role.IsArchived)
                    archived.Add(role.Name);
            }

            var current = await userManager.GetRolesAsync(user);
            var toRemove = current.Except(wanted, StringComparer.OrdinalIgnoreCase).ToList();
            var toAdd = wanted.Except(current, StringComparer.OrdinalIgnoreCase).ToList();

            // An archived role stays with those who hold it, but nobody new is given it.
            if (toAdd.FirstOrDefault(archived.Contains) is { } archivedRole)
                return IdentityResult.Failed(IdentityErrors.RoleArchived(archivedRole));

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
}
