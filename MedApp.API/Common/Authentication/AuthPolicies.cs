using System.Security.Claims;

namespace MedApp.API.Common.Authentication;

public static class AuthPolicies
{
    /// <summary>Only the user a <c>{username}</c> route names (<see cref="IsOwnUsername"/>).</summary>
    public const string SelfUserOnly = "SelfUserOnly";

    /// <summary>
    /// Whether the request's <c>{username}</c> route value is the signed-in user's own name, exactly (case and all).
    /// The <see cref="SelfUserOnly"/> policy and <see cref="PasswordChangeRequired"/> both ask this, so they agree.
    /// </summary>
    public static bool IsOwnUsername(HttpContext http, ClaimsPrincipal user)
    {
        var routeUsername = http.Request.RouteValues["username"]?.ToString();
        var userName = user.Identity?.Name;

        return routeUsername is not null
               && userName is not null
               && routeUsername == userName;
    }
}
