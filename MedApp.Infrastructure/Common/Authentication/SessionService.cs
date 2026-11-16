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
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(8);

    public async Task<LoginResult> CreateSessionAsync(
        Guid userId,
        string? ipAddress,
        string? userAgent,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var expiresAt = now.Add(SessionLifetime);
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

        var roles = await userManager.GetRolesAsync(user);

        return new SessionValidationResult(
            user.Id,
            user.UserName ?? string.Empty,
            roles.ToArray(),
            user.MustChangePassword);
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