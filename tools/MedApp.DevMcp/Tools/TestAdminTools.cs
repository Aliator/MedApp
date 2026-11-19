using System.ComponentModel;
using MedApp.Application.Common.Identity;
using MedApp.Infrastructure.Common.Identity;
using MedApp.Infrastructure.Data;
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

    /// <summary>
    /// Admin alone: admins are read only and in no team (MC3 of <c>docs/multi-clinic-plan.md</c>), as the light demo's
    /// admin is. Clinical work needs a member of staff in a team (<c>ensure_test_staff</c>).
    /// </summary>
    public static readonly string[] Roles = [AppRoles.Admin];

    [McpServerTool(Name = "ensure_test_admin", Idempotent = true)]
    [Description("Makes sure the local test login admin / admin exists, with the Admin role alone (any other is taken away: admins are read only; use ensure_test_staff for clinical work), and isn't locked out. Creates the account (and any missing standard role) if it's missing; otherwise puts the password back to 'admin'. It's never made to change its password. The password skips the app's password rules, so this is for local testing only. reset_demo_data runs it too.")]
    public static async Task<object> EnsureTestAdmin(
        UserManager<ApplicationUser> users,
        MedAppDbContext context,
        CancellationToken ct = default)
    {
        // Missing standard roles are added; ones an admin has edited locally are left as they are.
        await StandardRoleSetup.EnsureAsync(context, DateTime.UtcNow, resetPermissions: false, ct);

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
        // The test login signs in with admin / admin straight away, even if a reset in the app had asked it to change.
        user.MustChangePassword = false;
        Guards.Check(await users.UpdateAsync(user), $"Set the {Username} password");
        Guards.Check(await users.UpdateSecurityStampAsync(user), $"Refresh the {Username} security stamp");
        Guards.Check(await users.SetLockoutEndDateAsync(user, null), $"Unlock {Username}");
        Guards.Check(await users.ResetAccessFailedCountAsync(user), $"Reset {Username}'s failed sign-ins");

        var (missing, extra) = TestStaffTools.RoleChanges(await users.GetRolesAsync(user), Roles);
        if (missing.Count > 0)
            Guards.Check(await users.AddToRolesAsync(user, missing), $"Give {Username} its roles");
        if (extra.Count > 0)
            Guards.Check(await users.RemoveFromRolesAsync(user, extra), $"Take {Username}'s other roles away");

        return new { username = Username, password = Password, created, roles = Roles };
    }
}
