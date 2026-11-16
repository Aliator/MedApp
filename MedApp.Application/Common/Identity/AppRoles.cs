namespace MedApp.Application.Common.Identity;

/// <summary>The roles the app checks for by name (the API's <c>[Authorize(Roles = "Admin")]</c> uses the same).</summary>
public static class AppRoles
{
    public const string Admin = "Admin";

    public const string User = "User";

    /// <summary>The roles that make someone staff (the API's Staff policy): either one. Staff have calendars.</summary>
    public static readonly IReadOnlyList<string> Staff = [Admin, User];

    /// <summary>Whether these roles (names, any case) include a staff role.</summary>
    public static bool IncludesStaff(IEnumerable<string> roles) =>
        roles.Any(role => Staff.Contains(role, StringComparer.OrdinalIgnoreCase));
}
