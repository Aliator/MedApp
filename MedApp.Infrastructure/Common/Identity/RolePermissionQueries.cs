using MedApp.Application.Common.Identity;
using MedApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Common.Identity;

/// <summary>A user's roles (by name) and the permissions they add up to.</summary>
public sealed record UserRolesAndPermissions(IReadOnlyList<string> Roles, IReadOnlySet<Permission> Permissions, bool IsAdmin);

/// <summary>
/// Reads permissions from roles: a user's own, on every request (<see cref="Authentication.SessionService"/>), and who
/// holds one (to tell holders something, or check that someone may be given a task). Both follow
/// <see cref="Permissions.GrantedBy(bool, IEnumerable{string?})"/>: the built-in role has the Admin set; any other role
/// its stored, known, non-Admin-only permissions and <see cref="Permission.UseApp"/>.
/// </summary>
public static class RolePermissionQueries
{
    /// <summary>The user's roles, each with its stored permissions, in one query (an indexed join), with no cache.</summary>
    public static async Task<UserRolesAndPermissions> ForUserAsync(MedAppDbContext context, Guid userId, CancellationToken ct)
    {
        var rows = await (
                from userRole in context.UserRoles.AsNoTracking()
                where userRole.UserId == userId
                join role in context.Roles.AsNoTracking() on userRole.RoleId equals role.Id
                from permission in context.RolePermissions.AsNoTracking()
                    .Where(p => p.RoleId == role.Id)
                    .DefaultIfEmpty()
                select new { role.Id, role.Name, role.IsBuiltIn, Permission = permission == null ? null : permission.Permission })
            .ToListAsync(ct);

        var roles = rows
            .GroupBy(row => row.Id)
            .Select(group => new
            {
                Name = group.First().Name ?? string.Empty,
                Granted = Permissions.GrantedBy(group.First().IsBuiltIn, group.Select(row => row.Permission)),
                group.First().IsBuiltIn,
            })
            .ToList();

        return new UserRolesAndPermissions(
            roles.Select(role => role.Name).OrderBy(name => name, StringComparer.Ordinal).ToList(),
            Permissions.GrantedBy(roles.Select(role => role.Granted)),
            roles.Any(role => role.IsBuiltIn));
    }

    /// <summary>The IDs of the users who hold this permission through any of their roles (each once).</summary>
    public static IQueryable<Guid> HoldersOf(MedAppDbContext context, Permission permission)
    {
        var name = permission.ToString();
        var adminHasIt = Permissions.Admin.Contains(permission);
        var everyRoleHasIt = permission == Permission.UseApp;
        var editableRolesMayHaveIt = !Permissions.Describe(permission).IsAdminOnly;

        return (
                from userRole in context.UserRoles.AsNoTracking()
                join role in context.Roles.AsNoTracking() on userRole.RoleId equals role.Id
                where role.IsBuiltIn
                    ? adminHasIt
                    : everyRoleHasIt
                      || (editableRolesMayHaveIt && context.RolePermissions.Any(p => p.RoleId == role.Id && p.Permission == name))
                select userRole.UserId)
            .Distinct();
    }

    /// <summary>
    /// The IDs of the users who hold the built-in Admin role and can still sign in: not locked out now. The last of
    /// them can't lose the role or be deleted (<see cref="LastAdminGuard"/>).
    /// </summary>
    public static IQueryable<Guid> ActiveAdmins(MedAppDbContext context, DateTimeOffset now) =>
        (
            from userRole in context.UserRoles.AsNoTracking()
            join role in context.Roles.AsNoTracking() on userRole.RoleId equals role.Id
            join user in context.Users.AsNoTracking() on userRole.UserId equals user.Id
            where role.IsBuiltIn && (user.LockoutEnd == null || user.LockoutEnd <= now)
            select user.Id)
        .Distinct();
}
