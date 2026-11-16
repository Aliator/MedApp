using Microsoft.AspNetCore.Mvc;

namespace MedApp.API.Common.Authentication;

/// <summary>
/// Holds a user who must change the password an admin gave them (their session's
/// <see cref="SessionAuthenticationDefaults.MustChangePasswordClaim"/>) to that one task: every other <c>/api</c>
/// request, the notifications hub included, is refused with a 403 <see cref="ProblemDetails"/> whose <c>code</c> is
/// <see cref="Code"/>. They may only ask who they are, sign in again (which replaces the session), sign out, read their own UI settings (so the page shows in their
/// theme), and change their own password. Pages and static files pass, because the web app sends them to its
/// change-password page. Runs after authentication, so it sees the user, and before authorization.
/// </summary>
public sealed class PasswordChangeRequired(RequestDelegate next)
{
    public const string Title = "Password change required";
    public const string Code = "password_change_required";

    private const string ProblemJson = "application/problem+json";

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api")
            || context.User.Identity?.IsAuthenticated != true
            || !context.User.MustChangePassword()
            || IsAllowed(context))
        {
            await next(context);
            return;
        }

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = Title,
            Detail = "Choose your own password before doing anything else.",
            Extensions = { ["code"] = Code }
        };

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        // Given to the write itself: it sets the content type, and would replace one set before it.
        await context.Response.WriteAsJsonAsync(problem, options: null, contentType: ProblemJson);
    }

    /// <summary>The requests a user who must change their password may still make.</summary>
    private static bool IsAllowed(HttpContext context)
    {
        var request = context.Request;
        var path = request.Path.Value ?? string.Empty;

        if (HttpMethods.IsGet(request.Method))
            return Is(path, "/api/auth/whoami") || Is(path, "/api/auth/me/settings");

        // Signing in (as anyone) checks the posted password afresh and replaces this session, so it gives nothing
        // away; without it, a browser still holding a forced session couldn't sign in again. Signing out ends it.
        if (HttpMethods.IsPost(request.Method))
            return Is(path, "/api/auth/login") || Is(path, "/api/auth/logout");

        return HttpMethods.IsPut(request.Method) && IsOwnPasswordChange(context, path);
    }

    /// <summary>
    /// <c>PUT /api/auth/users/{username}/update-password</c> for the signed-in user's own name, compared as the
    /// <see cref="AuthPolicies.SelfUserOnly"/> policy compares it (the route's <c>username</c>, which routing has
    /// already matched by now).
    /// </summary>
    private static bool IsOwnPasswordChange(HttpContext context, string path)
    {
        var segments = path.Split('/');
        return segments.Length == 6
               && segments[0].Length == 0
               && Is(segments[1], "api")
               && Is(segments[2], "auth")
               && Is(segments[3], "users")
               && Is(segments[5], "update-password")
               && AuthPolicies.IsOwnUsername(context, context.User);
    }

    // Routing ignores case, so the allowlist does too.
    private static bool Is(string value, string expected) =>
        string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
}
