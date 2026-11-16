using MedApp.Contracts.Authentication.Responses;
using MedApp.Domain.Authentication;

namespace MedApp.Application.Authentication.Sessions.Common;

public static class SessionMapper
{
    /// <summary>
    /// A session's expiry as of <paramref name="utcNow"/>: when it ends, the whole seconds it has left (none once it
    /// has ended), and whether extending it would give it more time.
    /// </summary>
    /// <remarks>
    /// The expiry is marked UTC: read back from the database it has no kind, and an unmarked time would be taken as
    /// the server's local time, both in the JSON and as the cookie's expiry (which the controller takes from here).
    /// </remarks>
    public static SessionResponse ToResponse(UserSession session, DateTime utcNow) =>
        new(
            DateTime.SpecifyKind(session.ExpiresAtUtc, DateTimeKind.Utc),
            (int)Math.Max(0, Math.Floor((session.ExpiresAtUtc - utcNow).TotalSeconds)),
            session.CanExtend(utcNow));
}
