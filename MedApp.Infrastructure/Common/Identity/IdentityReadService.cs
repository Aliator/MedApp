using MedApp.Application.Authentication.Users.Queries.SearchUsers;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Paging;
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

    public async Task<PagedResult<UserResponse>> SearchUsersAsync(SearchUsersQuery query, CancellationToken ct)
    {
        IQueryable<ApplicationUser> users = userManager.Users;

        if (!string.IsNullOrWhiteSpace(query.Role))
        {
            var ids = (await userManager.GetUsersInRoleAsync(query.Role.Trim())).Select(u => u.Id).ToList();
            users = users.Where(u => ids.Contains(u.Id));
        }

        if (!string.IsNullOrWhiteSpace(query.Username))
        {
            // Identity's normalized names are upper case, so this ignores case in SQL and in memory alike.
            var term = userManager.NormalizeName(query.Username.Trim());
            users = users.Where(u => u.NormalizedUserName!.Contains(term));
        }

        // The users table is small (staff accounts), so these run synchronously, like the methods above.
        var totalCount = users.Count();

        var ordered = query.Ascending
            ? users.OrderBy(u => u.NormalizedUserName).ThenBy(u => u.Id)
            : users.OrderByDescending(u => u.NormalizedUserName).ThenBy(u => u.Id);

        var page = ordered
            .Skip(Paging.Skip(query.Page, query.PageSize))
            .Take(query.PageSize)
            .ToList();

        var items = new List<UserResponse>(page.Count);
        foreach (var user in page)
        {
            ct.ThrowIfCancellationRequested();
            var roles = await userManager.GetRolesAsync(user);
            items.Add(new UserResponse(user.Id, user.UserName!, roles.ToList()));
        }

        return new PagedResult<UserResponse>(items, totalCount);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetUsernamesByIdAsync(IEnumerable<Guid> userIds, CancellationToken ct)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0)
            return new Dictionary<Guid, string>();

        return await userManager.Users
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.UserName })
            .ToDictionaryAsync(u => u.Id, u => u.UserName!, ct);
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