using MedApp.Application.Common.Authentication;

namespace MedApp.API.Common.Authentication;

public interface ISessionCookieService
{
    void AppendSessionCookie(HttpResponse response, LoginResult session);
    void DeleteSessionCookie(HttpResponse response);
}