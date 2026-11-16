using System.ComponentModel;
using MediatR;
using MedApp.Application.Authentication.Roles.Commands.AssignRole;
using MedApp.Application.Authentication.Roles.Commands.RevokeRole;
using MedApp.Application.Authentication.Users.Commands.CreateUser;
using MedApp.Application.Common.Identity;
using MedApp.Infrastructure.Common.Identity;
using MedApp.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace MedApp.DevMcp.Tools;

/// <summary>Local dev accounts. Creation and role changes go through the app's own MediatR commands.</summary>
[McpServerToolType]
public sealed class AccountTools
{
    [McpServerTool(Name = "list_users", ReadOnly = true, Idempotent = true)]
    [Description("Lists every account with its roles, lockout state, failed sign-in count and number of active sessions. Protected accounts are flagged.")]
    public static async Task<object> ListUsers(UserManager<ApplicationUser> users, MedAppDbContext db, DevMcpOptions options, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var all = await users.Users.AsNoTracking().OrderBy(u => u.UserName).ToListAsync(ct);
        var sessions = await db.UserSessions.AsNoTracking()
            .Where(s => s.RevokedAtUtc == null && s.ExpiresAtUtc > now)
            .GroupBy(s => s.UserId).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        var result = new List<object>();
        foreach (var u in all)
        {
            result.Add(new
            {
                username = u.UserName,
                roles = await users.GetRolesAsync(u),
                isProtected = options.IsProtected(u.UserName!),
                lockedOutUntilUtc = u.LockoutEnd > DateTimeOffset.UtcNow ? u.LockoutEnd : null,
                failedSignIns = u.AccessFailedCount,
                activeSessions = sessions.GetValueOrDefault(u.Id),
                createdAt = u.CreatedAt,
            });
        }
        return result;
    }

    [McpServerTool(Name = "list_roles", ReadOnly = true, Idempotent = true)]
    [Description("Lists the roles that exist (e.g. Admin, User).")]
    public static async Task<IReadOnlyList<string?>> ListRoles(RoleManager<IdentityRole<Guid>> roles, CancellationToken ct) =>
        await roles.Roles.AsNoTracking().OrderBy(r => r.Name).Select(r => r.Name).ToListAsync(ct);

    [McpServerTool(Name = "create_user")]
    [Description("Creates a local dev account and gives it the listed roles. If no password is given, the app generates one (as it does for accounts created in the UI), and it's returned. Either way the account can sign in with it straight away: unlike an account an admin creates in the app, it isn't made to change its password first. All or nothing: if any step fails, no account is left behind. Use synthetic usernames only (e.g. dev-admin, dev-clinician).")]
    public static async Task<object> CreateUser(
        IMediator mediator,
        UserManager<ApplicationUser> users,
        RoleManager<IdentityRole<Guid>> roleManager,
        IIdentityTransactions transactions,
        [Description("Username for the new account.")] string username,
        [Description("Roles to give it, e.g. [\"Admin\"] or [\"User\"]. Must already exist.")] string[]? roles = null,
        [Description("Password to set. Omit to generate one (returned in the result).")] string? password = null,
        CancellationToken ct = default)
    {
        roles ??= [];
        foreach (var role in roles)
            if (!await roleManager.RoleExistsAsync(role))
                throw new McpException($"Role '{role}' does not exist. Use list_roles.");

        // A chosen password is checked against the password rules first, for a clear error before anything is made.
        if (password is not null)
            Guards.Check(await Passwords.Validate(users, username, password), "Check password");

        var created = await CreateAccount(mediator, users, transactions, username, roles, password, ct);
        return new { username, roles, password = created.Password };
    }

    /// <summary>A dev account <see cref="CreateAccount"/> made, with the password it signs in with.</summary>
    internal sealed record CreatedAccount(Guid Id, string Password);

    /// <summary>
    /// Makes a dev account through the app's own commands (the password rules, the username rules, the roles), with
    /// the given password or the one the app generates, and the roles. The app makes an account it creates change its
    /// generated password at first sign-in; a dev account isn't, so it can sign in at once with the password returned.
    /// It all runs in one transaction: if any step fails or throws (a database error, a cancellation), the transaction
    /// is rolled back, so no half-made account is left behind. The work loads everything it changes itself, so a retry
    /// after a transient error starts clean.
    /// </summary>
    internal static async Task<CreatedAccount> CreateAccount(
        IMediator mediator,
        UserManager<ApplicationUser> users,
        IIdentityTransactions transactions,
        string username,
        IReadOnlyCollection<string> roles,
        string? password,
        CancellationToken ct)
    {
        CreatedAccount? account = null;
        var result = await transactions.RunAsync(async () =>
        {
            // The app's command generates the first password; a chosen one then replaces it.
            var created = await Guards.Validated(() => mediator.Send(new CreateUserCommand(username), ct));
            if (!created.Result.Succeeded)
                return created.Result;

            var user = await users.FindByNameAsync(username) ?? throw new McpException($"{username} was not created.");
            // Saved by the update below, with the chosen password if there is one.
            user.MustChangePassword = false;

            IdentityResult saved;
            if (password is null)
            {
                saved = await users.UpdateAsync(user);
            }
            else
            {
                var token = await users.GeneratePasswordResetTokenAsync(user);
                saved = await users.ResetPasswordAsync(user, token, password);
            }

            if (!saved.Succeeded)
                return saved;

            foreach (var role in roles)
            {
                var assigned = await Guards.Validated(() => mediator.Send(new AssignRoleCommand(username, role), ct));
                if (assigned is null)
                    return IdentityResult.Failed(new IdentityError { Description = $"Couldn't give {username} the role {role}." });
            }

            account = new CreatedAccount(user.Id, password ?? created.GeneratedPassword!);
            return IdentityResult.Success;
        }, ct);

        Guards.Check(result, $"Create {username}");
        return account!;
    }

    [McpServerTool(Name = "set_password")]
    [Description("Sets a new password for an account and clears any lockout. If no password is given, a random one is generated and returned. The account can sign in with it straight away: it isn't made to change it, even if an admin's reset had asked it to. Refuses protected accounts.")]
    public static async Task<object> SetPassword(
        UserManager<ApplicationUser> users,
        DevMcpOptions options,
        [Description("Account to change.")] string username,
        [Description("New password. Omit to generate one (returned in the result).")] string? password = null)
    {
        Guards.RequireNotProtected(options, username, "change the password of");
        var user = await users.FindByNameAsync(username) ?? throw new McpException($"No account named '{username}'.");

        password ??= Passwords.Generate();
        // Saved with the new password: the password is chosen here, not handed on by an admin, so no change is needed.
        user.MustChangePassword = false;
        var token = await users.GeneratePasswordResetTokenAsync(user);
        Guards.Check(await users.ResetPasswordAsync(user, token, password), "Set password");
        await users.SetLockoutEndDateAsync(user, null);
        await users.ResetAccessFailedCountAsync(user);
        return new { username, password };
    }

    [McpServerTool(Name = "set_roles", Idempotent = true)]
    [Description("Makes an account's roles exactly the given list, assigning and revoking as needed. Protected accounts can gain roles but not lose any.")]
    public static async Task<object> SetRoles(
        IMediator mediator,
        UserManager<ApplicationUser> users,
        RoleManager<IdentityRole<Guid>> roleManager,
        DevMcpOptions options,
        [Description("Account to change.")] string username,
        [Description("The complete list of roles the account should have, e.g. [\"Admin\", \"User\"].")] string[] roles,
        CancellationToken ct = default)
    {
        var user = await users.FindByNameAsync(username) ?? throw new McpException($"No account named '{username}'.");
        foreach (var role in roles)
            if (!await roleManager.RoleExistsAsync(role))
                throw new McpException($"Role '{role}' does not exist. Use list_roles.");

        var current = await users.GetRolesAsync(user);
        var toAdd = roles.Except(current, StringComparer.OrdinalIgnoreCase).ToList();
        var toRemove = current.Except(roles, StringComparer.OrdinalIgnoreCase).ToList();
        if (toRemove.Count > 0) Guards.RequireNotProtected(options, username, "remove roles from");

        foreach (var role in toAdd) await Guards.Validated(() => mediator.Send(new AssignRoleCommand(username, role), ct));
        foreach (var role in toRemove) await Guards.Validated(() => mediator.Send(new RevokeRoleCommand(username, role), ct));
        return new { username, roles = await users.GetRolesAsync(user), added = toAdd, removed = toRemove };
    }

    [McpServerTool(Name = "unlock_user", Idempotent = true)]
    [Description("Clears an account's lockout and failed sign-in count.")]
    public static async Task<object> UnlockUser(UserManager<ApplicationUser> users, [Description("Account to unlock.")] string username)
    {
        var user = await users.FindByNameAsync(username) ?? throw new McpException($"No account named '{username}'.");
        await users.SetLockoutEndDateAsync(user, null);
        await users.ResetAccessFailedCountAsync(user);
        return new { username, unlocked = true };
    }

    [McpServerTool(Name = "delete_user", Destructive = true)]
    [Description("Deletes an account. Refuses protected accounts. Needs confirm: true.")]
    public static async Task<object> DeleteUser(
        IIdentityUserService userService,
        DevMcpOptions options,
        [Description("Account to delete.")] string username,
        [Description("Must be true to delete.")] bool confirm = false,
        CancellationToken ct = default)
    {
        Guards.RequireNotProtected(options, username, "delete");
        Guards.RequireConfirm(confirm, $"Deleting '{username}'");
        // Straight to the user service, not DeleteUserCommand: in the app an admin confirms a delete with their own
        // password, but this local tool has no signed-in admin; confirm: true is its confirmation.
        Guards.Check(await userService.DeleteUserAsync(username, ct), "Delete user");
        return new { username, deleted = true };
    }
}
