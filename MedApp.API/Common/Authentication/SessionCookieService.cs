using MedApp.Application.Common.Authentication;

namespace MedApp.API.Common.Authentication;

public sealed class SessionCookieService(IWebHostEnvironment environment) : ISessionCookieService
{
    public string CookieName { get; } = environment.IsDevelopment()
        ? SessionAuthenticationDefaults.DevelopmentCookieName
        : SessionAuthenticationDefaults.HostCookieName;

    public void AppendSessionCookie(HttpResponse response, LoginResult session)
    {
        response.Cookies.Append(
            CookieName,
            session.SessionId.ToString(),
            BuildCookieOptions(session.ExpiresAtUtc));
    }

    public void DeleteSessionCookie(HttpResponse response)
    {
        response.Cookies.Delete(
            CookieName,
            BuildCookieOptions());
    }

    public bool TryGetSessionCookie(HttpRequest request, out Guid? sessionId)
    {
        sessionId = null;
        if (!request.Cookies.TryGetValue(CookieName, out var raw))
            return false;

        if (Guid.TryParse(raw, out var parsed))
            sessionId = parsed;
        return true;
    }

    /// <summary>
    /// The UI is served from the API's own origin (and through a same-origin proxy in development), so the cookie
    /// is always first-party: Lax is enough, and blocks it on cross-site POSTs. Outside development it is Secure, for
    /// the whole site (Path=/) and this host only (no Domain), as its <c>__Host-</c> name requires. The Vite dev
    /// server is plain http://localhost, so there it's neither Secure nor prefixed.
    /// </summary>
    private CookieOptions BuildCookieOptions(DateTime? expiresAtUtc = null)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = !environment.IsDevelopment(),
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Domain = null,
            Expires = expiresAtUtc
        };
    }
}
