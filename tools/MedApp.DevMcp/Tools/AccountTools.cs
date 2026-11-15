using System.ComponentModel;
using MediatR;
using MedApp.Application.Authentication.Roles.Commands.AssignRole;
using MedApp.Application.Authentication.Roles.Commands.RevokeRole;
using MedApp.Application.Authentication.Users.Commands.CreateUser;
using MedApp.Application.Authentication.Users.Commands.DeleteUser;
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
    [Description("Creates a local dev account and gives it the listed roles. If no password is given, a random one is generated and returned. Use synthetic usernames only (e.g. dev-admin, dev-clinician).")]
    public static async Task<object> CreateUser(
        IMediator mediator,
        RoleManager<IdentityRole<Guid>> roleManager,
        [Description("Username for the new account.")] string username,
        [Description("Roles to give it, e.g. [\"Admin\"] or [\"User\"]. Must already exist.")] string[]? roles = null,
        [Description("Password to set. Omit to generate one (returned in the result).")] string? password = null,
        CancellationToken ct = default)
    {
        roles ??= [];
        foreach (var role in roles)
            if (!await roleManager.RoleExistsAsync(role))
                throw new McpException($"Role '{role}' does not exist. Use list_roles.");

        password ??= Passwords.Generate();
        Guards.Check(await Guards.Validated(() => mediator.Send(new CreateUserCommand(username, password), ct)), "Create user");
        foreach (var role in roles)
            await Guards.Validated(() => mediator.Send(new AssignRoleCommand(username, role), ct));

        return new { username, roles, password };
    }

    [McpServerTool(Name = "set_password")]
    [Description("Sets a new password for an account and clears any lockout. If no password is given, a random one is generated and returned. Refuses protected accounts.")]
    public static async Task<object> SetPassword(
        UserManager<ApplicationUser> users,
        DevMcpOptions options,
        [Description("Account to change.")] string username,
        [Description("New password. Omit to generate one (returned in the result).")] string? password = null)
    {
        Guards.RequireNotProtected(options, username, "change the password of");
        var user = await users.FindByNameAsync(username) ?? throw new McpException($"No account named '{username}'.");

        password ??= Passwords.Generate();
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
        IMediator mediator,
        DevMcpOptions options,
        [Description("Account to delete.")] string username,
        [Description("Must be true to delete.")] bool confirm = false,
        CancellationToken ct = default)
    {
        Guards.RequireNotProtected(options, username, "delete");
        Guards.RequireConfirm(confirm, $"Deleting '{username}'");
        Guards.Check(await Guards.Validated(() => mediator.Send(new DeleteUserCommand(username), ct)), "Delete user");
        return new { username, deleted = true };
    }
}
