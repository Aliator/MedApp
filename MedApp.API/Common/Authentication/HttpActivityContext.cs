using System.Security.Claims;
using MedApp.Application.Activity.Common;

namespace MedApp.API.Common.Authentication;

/// <summary>
/// The activity log's actor for the current HTTP request: the signed-in user from the session's claims, and the
/// address the request came from as Kestrel saw it (behind a proxy, or for a page rendered on the server, that's the
/// proxy's or the page server's address).
/// </summary>
public sealed class HttpActivityContext(IHttpContextAccessor accessor) : IActivityContext
{
    public ActivityActor Actor => From(accessor.HttpContext);

    public static ActivityActor From(HttpContext? http)
    {
        var user = http?.User;
        var isSignedIn = user?.Identity?.IsAuthenticated == true;
        Guid? userId = isSignedIn && Guid.TryParse(user!.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

        return new ActivityActor(
            userId,
            isSignedIn ? user!.Identity!.Name ?? string.Empty : string.Empty,
            http?.Connection.RemoteIpAddress?.ToString());
    }
}
