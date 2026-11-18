using MedApp.Application.Common.Identity;
using MedApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Common.Identity;

/// <summary>
/// Makes sure the standard roles (<see cref="StandardRoles"/>) exist, as the migration <c>AddedRolePermissions</c>
/// makes them, and can put their permissions back as they were. For local databases and tests only: medapp-dev's
/// <c>ensure_test_admin</c> and <c>reset_demo_data</c>, and the integration tests after emptying the database. A
/// deployed database gets its roles from the migration, and admins change them from then on.
/// </summary>
public static class StandardRoleSetup
{
    /// <summary>
    /// Adds any standard role that's missing (with its fixed ID and permissions), keeps Admin built in, and, with
    /// <paramref name="resetPermissions"/>, makes every other standard role's permissions, description and archived flag
    /// exactly as they start. Holders of the roles are left as they are.
    /// </summary>
    public static async Task EnsureAsync(MedAppDbContext context, DateTime nowUtc, bool resetPermissions, CancellationToken ct)
    {
        foreach (var standard in StandardRoles.All)
        {
            var normalized = standard.Name.ToUpperInvariant();
            var role = await context.Roles
                           .Include(r => r.Permissions)
                           .FirstOrDefaultAsync(r => r.NormalizedName == normalized, ct)
                       ?? await context.Roles
                           .Include(r => r.Permissions)
                           .FirstOrDefaultAsync(r => r.Id == standard.Id, ct);

            // A database from before roles: its User role is "All staff (before roles)", renamed as the migration does.
            if (role is null && standard == StandardRoles.AllStaffBeforeRoles)
                role = await context.Roles.Include(r => r.Permissions).FirstOrDefaultAsync(r => r.NormalizedName == "USER", ct);

            var isNew = role is null;
            role ??= new ApplicationRole
            {
                Id = standard.Id,
                ConcurrencyStamp = Guid.NewGuid().ToString(),
                CreatedAt = nowUtc,
            };

            if (isNew || resetPermissions)
            {
                role.Name = standard.Name;
                role.NormalizedName = normalized;
                role.Description = standard.Description;
                role.IsArchived = false;
                role.LastUpdated = nowUtc;

                // Admin's permissions are fixed in code, so it has no rows. Changed by difference, so a row kept isn't
                // deleted and added again (one key, tracked twice).
                var wanted = standard == StandardRoles.Admin
                    ? []
                    : standard.Permissions.Select(p => p.ToString()).ToHashSet(StringComparer.Ordinal);
                role.Permissions.RemoveAll(p => !wanted.Contains(p.Permission));
                foreach (var name in wanted.Except(role.Permissions.Select(p => p.Permission)).Order(StringComparer.Ordinal))
                    role.Permissions.Add(new RolePermission { RoleId = role.Id, Permission = name });
            }

            role.IsBuiltIn = standard == StandardRoles.Admin;

            if (isNew)
                context.Roles.Add(role);
        }

        await context.SaveChangesAsync(ct);
    }
}
