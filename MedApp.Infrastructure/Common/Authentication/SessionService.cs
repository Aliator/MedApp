using MedApp.Application.Common.Authentication;
using MedApp.Domain.Authentication;
using MedApp.Infrastructure.Common.Identity;
using MedApp.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Common.Authentication;

public sealed class SessionService(
    MedAppDbContext context,
    UserManager<ApplicationUser> userManager)
    : ISessionService
{
    public async Task<LoginResult> CreateSessionAsync(
        Guid userId,
        string? ipAddress,
        string? userAgent,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var expiresAt = now.Add(UserSession.Lifetime);
        var session = new UserSession(userId, now, expiresAt, ipAddress, userAgent);

        context.UserSessions.Add(session);
        await context.SaveChangesAsync(ct);

        return new LoginResult(session.Id, session.ExpiresAtUtc);
    }

    public async Task<SessionValidationResult?> ValidateSessionAsync(
        Guid sessionId,
        CancellationToken ct = default)
    {
        var session = await context.UserSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == sessionId, ct);

        if (session is null)
            return null;

        if (!session.IsActive(DateTime.UtcNow))
            return null;

        var user = await userManager.FindByIdAsync(session.UserId.ToString());
        if (user is null)
            return null;

        // The roles and what they allow, in one query, on every request: a change to a role, or to who holds it, applies
        // on each person's next request. No cache, so removing access is never delayed.
        var access = await RolePermissionQueries.ForUserAsync(context, user.Id, ct);
        // Their teams too, for their team walls: a change to someone's teams applies on their next request.
        var teamIds = await context.TeamMemberships
            .AsNoTracking()
            .Where(m => m.UserId == user.Id)
            .Select(m => m.TeamId)
            .OrderBy(id => id)
            .ToListAsync(ct);

        return new SessionValidationResult(
            user.Id,
            user.UserName ?? string.Empty,
            access.Roles.ToArray(),
            user.MustChangePassword,
            await MustSetUpTwoFactorAsync(user, access.IsAdmin, ct),
            access.Permissions,
            teamIds);
    }

    /// <summary>
    /// Whether the user is an admin (holds the built-in Admin role) without an authenticator app (two-factor off, or on with codes by email, which the
    /// requirement doesn't accept) while admins must use it, so they're held to setting the app up
    /// (<c>TwoFactorSetupRequired</c>). The setting is read only for such an admin, so other requests cost nothing more.
    /// </summary>
    private async Task<bool> MustSetUpTwoFactorAsync(ApplicationUser user, bool isAdmin, CancellationToken ct) =>
        user.EffectiveTwoFactorMethod != TwoFactorMethod.Authenticator
        && isAdmin
        && await context.SecuritySettings
            .AsNoTracking()
            .AnyAsync(settings => settings.Id == SecuritySettings.SingletonId && settings.RequireTwoFactorForAdmins, ct);

    public async Task<UserSession?> GetActiveSessionAsync(
        Guid sessionId,
        DateTime utcNow,
        CancellationToken ct = default)
    {
        var session = await context.UserSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == sessionId, ct);

        return session is not null && session.IsActive(utcNow) ? session : null;
    }

    public async Task<UserSession?> ExtendSessionAsync(
        Guid sessionId,
        DateTime utcNow,
        CancellationToken ct = default)
    {
        var session = await context.UserSessions
            .FirstOrDefaultAsync(candidate => candidate.Id == sessionId, ct);

        if (session is null || !session.IsActive(utcNow))
            return null;

        session.Extend(utcNow);
        await context.SaveChangesAsync(ct);

        return session;
    }

    public async Task RevokeSessionAsync(
        Guid sessionId,
        CancellationToken ct = default)
    {
        var session = await context.UserSessions
            .FirstOrDefaultAsync(candidate => candidate.Id == sessionId, ct);

        if (session is null)
            return;

        session.Revoke(DateTime.UtcNow);
        await context.SaveChangesAsync(ct);
    }

    public async Task<int> RevokeUserSessionsAsync(
        Guid userId,
        Guid? exceptSessionId = null,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var sessions = await context.UserSessions
            .Where(candidate => candidate.UserId == userId
                                && candidate.RevokedAtUtc == null
                                && candidate.ExpiresAtUtc > now
                                && (exceptSessionId == null || candidate.Id != exceptSessionId))
            .ToListAsync(ct);

        if (sessions.Count == 0)
            return 0;

        foreach (var session in sessions)
            session.Revoke(now);

        await context.SaveChangesAsync(ct);
        return sessions.Count;
    }
}