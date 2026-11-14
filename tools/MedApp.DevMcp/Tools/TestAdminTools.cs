using System.ComponentModel;
using MedApp.Infrastructure.Common.Identity;
using Microsoft.AspNetCore.Identity;
using ModelContextProtocol.Server;

namespace MedApp.DevMcp.Tools;

/// <summary>
/// The local test login, admin / admin. The password is far too weak for the app's password rules, so it's set
/// as a hash directly rather than through the app's commands. That's only acceptable because medapp-dev refuses
/// to run against anything but a local database; never create this account anywhere else.
/// </summary>
[McpServerToolType]
public sealed class TestAdminTools
{
    public const string Username = "admin";
    public const string Password = "admin";

    private static readonly string[] Roles = ["Admin", "User"];

    [McpServerTool(Name = "ensure_test_admin", Idempotent = true)]
    [Description("Makes sure the local test login admin / admin exists, with the Admin and User roles, and isn't locked out. Creates the account if it's missing; otherwise puts the password back to 'admin'. The password skips the app's password rules, so this is for local testing only. reset_demo_data runs it too.")]
    public static async Task<object> EnsureTestAdmin(
        UserManager<ApplicationUser> users,
        RoleManager<IdentityRole<Guid>> roles,
        CancellationToken ct = default)
    {
        foreach (var role in Roles)
            if (!await roles.RoleExistsAsync(role))
                Guards.Check(await roles.CreateAsync(new IdentityRole<Guid>(role)), $"Create role {role}");

        var user = await users.FindByNameAsync(Username);
        var created = user is null;
        if (user is null)
        {
            user = new ApplicationUser { UserName = Username, CreatedAt = DateTime.UtcNow };
            // No password here: CreateAsync(user, password) would run the password rules, which "admin" fails.
            Guards.Check(await users.CreateAsync(user), $"Create {Username}");
        }

        ct.ThrowIfCancellationRequested();
        user.PasswordHash = users.PasswordHasher.HashPassword(user, Password);
        Guards.Check(await users.UpdateAsync(user), $"Set the {Username} password");
        Guards.Check(await users.UpdateSecurityStampAsync(user), $"Refresh the {Username} security stamp");
        Guards.Check(await users.SetLockoutEndDateAsync(user, null), $"Unlock {Username}");
        Guards.Check(await users.ResetAccessFailedCountAsync(user), $"Reset {Username}'s failed sign-ins");

        var missing = Roles.Except(await users.GetRolesAsync(user), StringComparer.OrdinalIgnoreCase).ToArray();
        if (missing.Length > 0)
            Guards.Check(await users.AddToRolesAsync(user, missing), $"Give {Username} its roles");

        return new { username = Username, password = Password, created, roles = Roles };
    }
}
