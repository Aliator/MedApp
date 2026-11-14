using System.ComponentModel;
using MedApp.Infrastructure.Common.Identity;
using MedApp.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace MedApp.DevMcp.Tools;

[McpServerToolType]
public sealed class SessionTools
{
    [McpServerTool(Name = "list_sessions", ReadOnly = true, Idempotent = true)]
    [Description("Lists sign-in sessions (newest first), optionally for one account. Active only unless includeInactive is true.")]
    public static async Task<object> ListSessions(
        MedAppDbContext db,
        [Description("Only this account's sessions.")] string? username = null,
        [Description("Include expired and revoked sessions.")] bool includeInactive = false,
        [Description("Maximum rows, 1-200.")] int take = 50,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var query =
            from s in db.UserSessions.AsNoTracking()
            join u in db.Set<ApplicationUser>().AsNoTracking() on s.UserId equals u.Id
            select new { s, u.UserName };
        if (username is not null) query = query.Where(x => x.UserName == username);
        if (!includeInactive) query = query.Where(x => x.s.RevokedAtUtc == null && x.s.ExpiresAtUtc > now);

        return await query
            .OrderByDescending(x => x.s.CreatedAtUtc)
            .Take(Math.Clamp(take, 1, 200))
            .Select(x => new
            {
                username = x.UserName,
                createdAtUtc = x.s.CreatedAtUtc,
                expiresAtUtc = x.s.ExpiresAtUtc,
                revokedAtUtc = x.s.RevokedAtUtc,
                active = x.s.RevokedAtUtc == null && x.s.ExpiresAtUtc > now,
                userAgent = x.s.UserAgent,
            })
            .ToListAsync(ct);
    }

    [McpServerTool(Name = "revoke_sessions", Idempotent = true)]
    [Description("Signs an account out everywhere by revoking all its active sessions.")]
    public static async Task<object> RevokeSessions(
        MedAppDbContext db,
        UserManager<ApplicationUser> users,
        [Description("Account to sign out.")] string username,
        CancellationToken ct = default)
    {
        var user = await users.FindByNameAsync(username) ?? throw new McpException($"No account named '{username}'.");
        var now = DateTime.UtcNow;
        var active = await db.UserSessions
            .Where(s => s.UserId == user.Id && s.RevokedAtUtc == null && s.ExpiresAtUtc > now)
            .ToListAsync(ct);
        foreach (var session in active) session.Revoke(now);
        await db.SaveChangesAsync(ct);
        return new { username, revoked = active.Count };
    }
}
