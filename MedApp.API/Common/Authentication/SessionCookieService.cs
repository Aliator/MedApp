using MedApp.Application.Common.Authentication;

namespace MedApp.API.Common.Authentication;

public sealed class SessionCookieService : ISessionCookieService
{
    public void AppendSessionCookie(HttpResponse response, SessionToken session)
    {
        response.Cookies.Append(
            SessionAuthenticationDefaults.CookieName,
            session.SessionId.ToString(),
            BuildCookieOptions(session.ExpiresAtUtc));
    }

    public void DeleteSessionCookie(HttpResponse response)
    {
        response.Cookies.Delete(
            SessionAuthenticationDefaults.CookieName,
            BuildCookieOptions());
    }

    private static CookieOptions BuildCookieOptions(
        DateTime? expiresAtUtc = null)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            Expires = expiresAtUtc
        };
    }
}