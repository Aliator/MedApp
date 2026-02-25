using MedApp.Application.Common.Identity;
using MedApp.Contracts.Authentication.Responses;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Common.Identity;

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
        if (user is null) return [];

        var roles = await userManager.GetRolesAsync(user);
        return roles.ToList();
    }

    public async Task<UserResponse?> GetUserAsync(
        string username,
        CancellationToken ct)
    {
        var user = await userManager.FindByNameAsync(username);
        if (user is null) return null;

        var roles = await userManager.GetRolesAsync(user);
        return new UserResponse(user.Id, user.UserName!, roles.ToList());
    }

    public async Task<IReadOnlyList<UserResponse>> GetUsersAsync(CancellationToken ct)
    {
        var users = await userManager.Users
            .Select(u => new { u.Id, u.UserName })
            .ToListAsync(ct);

        var results = new List<UserResponse>(users.Count);

        foreach (var u in users)
        {
            var user = await userManager.FindByIdAsync(u.Id.ToString());
            if (user is null) continue;

            var roles = await userManager.GetRolesAsync(user);
            results.Add(new UserResponse(user.Id, user.UserName!, roles.ToList()));
        }

        return results;
    }
}