namespace MedApp.Application.Common.Identity;

/// <summary>
/// The role names code knows. Authorisation never checks them: endpoints and handlers check permissions
/// (<see cref="Permission"/>). Only the built-in Admin role is special: its fixed permissions, the two-factor
/// requirement for admins, and the rule that one active admin always remains.
/// </summary>
public static class AppRoles
{
    /// <summary>The built-in role. It can't be renamed, edited, archived or deleted.</summary>
    public const string Admin = "Admin";

    /// <summary>The old <c>User</c> role's name since roles came (<see cref="StandardRoles.AllStaffBeforeRoles"/>).</summary>
    public const string AllStaffBeforeRoles = "All staff (before roles)";

    /// <summary>
    /// Whether someone holding these roles is staff: any role at all, since every role grants
    /// <see cref="Permission.UseApp"/>. Staff have calendars.
    /// </summary>
    public static bool IncludesStaff(IEnumerable<string> roles) => roles.Any();
}
