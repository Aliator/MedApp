using MedApp.Application.Common.Authentication;

namespace MedApp.API.Common.Authentication;

public sealed class SessionCookieService(IWebHostEnvironment environment) : ISessionCookieService
{
    public void AppendSessionCookie(HttpResponse response, LoginResult session)
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

    /// <summary>
    /// The UI is served from the API's own origin (and through a same-origin proxy in development), so the cookie
    /// is always first-party: Lax is enough, and blocks it on cross-site POSTs. It is Secure outside development,
    /// where the app is served over HTTPS; the Vite dev server is plain http://localhost.
    /// </summary>
    private CookieOptions BuildCookieOptions(DateTime? expiresAtUtc = null)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = !environment.IsDevelopment(),
            SameSite = SameSiteMode.Lax,
            Expires = expiresAtUtc
        };
    }
}