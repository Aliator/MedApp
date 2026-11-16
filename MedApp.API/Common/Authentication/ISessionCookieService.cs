using MedApp.Application.Common.Authentication;

namespace MedApp.API.Common.Authentication;

public interface ISessionCookieService
{
    /// <summary>
    /// The session cookie's name here: <see cref="SessionAuthenticationDefaults.HostCookieName"/>, or
    /// <see cref="SessionAuthenticationDefaults.DevelopmentCookieName"/> in Development.
    /// </summary>
    string CookieName { get; }

    void AppendSessionCookie(HttpResponse response, LoginResult session);
    void DeleteSessionCookie(HttpResponse response);

    /// <summary>
    /// The request's session cookie: false if it has none, otherwise true, with the ID in <paramref name="sessionId"/>
    /// (or null if the cookie isn't one).
    /// </summary>
    bool TryGetSessionCookie(HttpRequest request, out Guid? sessionId);
}
