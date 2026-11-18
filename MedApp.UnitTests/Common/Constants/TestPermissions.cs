using MedApp.Application.Common.Identity;

namespace MedApp.UnitTests.Common.Constants;

/// <summary>Permission sets for tests: what the standard roles grant.</summary>
public static class TestPermissions
{
    /// <summary>The built-in Admin role's fixed set.</summary>
    public static IReadOnlySet<Permission> Admin => Permissions.Admin;

    /// <summary>What every member of staff could do before roles ("All staff (before roles)", the old User role).</summary>
    public static IReadOnlySet<Permission> Staff => StandardRoles.AllStaffBeforeRoles.Permissions;

    /// <summary>An admin's or a member of staff's permissions, as before roles.</summary>
    public static IReadOnlySet<Permission> For(bool isAdmin) => isAdmin ? Admin : Staff;

    /// <summary>
    /// What these roles grant together: a standard role's permissions by its name ("User" for the old role, now "All
    /// staff (before roles)"), and UseApp for any other role. No roles, nothing.
    /// </summary>
    public static IReadOnlySet<Permission> ForRoles(params string[] roles) =>
        Permissions.GrantedBy(roles.Select(role =>
            role == "User"
                ? Staff
                : StandardRoles.All.FirstOrDefault(r => r.Name == role)?.Permissions
                  ?? new HashSet<Permission> { Permission.UseApp }));
}
