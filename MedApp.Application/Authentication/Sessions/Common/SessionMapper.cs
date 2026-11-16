using MedApp.Contracts.Authentication.Responses;
using MedApp.Domain.Authentication;

namespace MedApp.Application.Authentication.Sessions.Common;

public static class SessionMapper
{
    /// <summary>
    /// A session's expiry as of <paramref name="utcNow"/>: when it ends, the whole seconds it has left (none once it
    /// has ended), and whether extending it would give it more time.
    /// </summary>
    public static SessionResponse ToResponse(UserSession session, DateTime utcNow) =>
        new(
            session.ExpiresAtUtc,
            (int)Math.Max(0, Math.Floor((session.ExpiresAtUtc - utcNow).TotalSeconds)),
            session.CanExtend(utcNow));
}
