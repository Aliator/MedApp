using MedApp.Application.Common.Authentication;

namespace MedApp.API.Common.Authentication;

public sealed class SessionCookieService(IWebHostEnvironment environment) : ISessionCookieService
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

    private CookieOptions BuildCookieOptions(DateTime? expiresAtUtc = null)
    {
        var isHttps = environment.IsProduction() || environment.IsStaging();

        return new CookieOptions
        {
            HttpOnly = true,
            Secure = isHttps,
            SameSite = isHttps ? SameSiteMode.None : SameSiteMode.Lax,
            Expires = expiresAtUtc
        };
    }
}