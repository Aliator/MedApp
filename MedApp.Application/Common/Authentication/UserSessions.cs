using MedApp.Application.Common.Identity;

namespace MedApp.Application.Common.Authentication;

/// <summary>Ending a user's sessions when their account changes, found by username.</summary>
public static class UserSessions
{
    /// <summary>
    /// Ends every active session of <paramref name="username"/>, except <paramref name="exceptSessionId"/> (the session
    /// making the change, when it's theirs). Returns false, ending nothing, if there's no such user.
    /// </summary>
    public static async Task<bool> EndSessionsAsync(
        this ISessionService sessions,
        IIdentityReadService users,
        string username,
        Guid? exceptSessionId,
        CancellationToken ct)
    {
        var user = await users.GetUserAsync(username, ct);
        if (user is null)
            return false;

        await sessions.RevokeUserSessionsAsync(user.UserId, exceptSessionId, ct);
        return true;
    }
}
