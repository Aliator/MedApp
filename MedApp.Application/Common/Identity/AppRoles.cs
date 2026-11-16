namespace MedApp.Application.Common.Identity;

/// <summary>The roles the app checks for by name (the API's <c>[Authorize(Roles = "Admin")]</c> uses the same).</summary>
public static class AppRoles
{
    public const string Admin = "Admin";
}
