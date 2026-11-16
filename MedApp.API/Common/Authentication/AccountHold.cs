using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace MedApp.API.Common.Authentication;

/// <summary>
/// Holds a signed-in user to one task before anything else (the pattern of <see cref="PasswordChangeRequired"/> and
/// <see cref="TwoFactorSetupRequired"/>): while <see cref="Applies"/>, every <c>/api</c> request the hold doesn't allow,
/// the notifications hub included, is refused with a 403 <see cref="ProblemDetails"/> whose <c>code</c> names the task,
/// and the web app sends its pages there. Pages and static files pass. Each hold runs after authentication, so it sees
/// the user, and before authorization.
/// <para>
/// Every hold allows asking who you are, reading your own UI settings (so the page shows in your theme), signing in
/// again (which checks afresh and replaces the session) and out, the endpoints used before signing in (sign-in's
/// features, a forgotten password and its reset), which never use the session, so a browser still holding a held session
/// can open an emailed link, and confirming an email address, which needs the session of the account the link is for (so
/// someone signing in for the first time, held to choose a password, can confirm the address an admin gave them) and
/// changes nothing else.
/// </para>
/// </summary>
public abstract class AccountHold(RequestDelegate next)
{
    private const string ProblemJson = "application/problem+json";

    /// <summary>The problem's title.</summary>
    protected abstract string HoldTitle { get; }

    /// <summary>The problem's <c>code</c>, which the web app recognises.</summary>
    protected abstract string HoldCode { get; }

    protected abstract string HoldDetail { get; }

    /// <summary>Whether this signed-in user is held.</summary>
    protected abstract bool Applies(ClaimsPrincipal user);

    /// <summary>The requests the task itself needs, beyond those every hold allows.</summary>
    protected abstract bool AllowsForTask(HttpContext context, string path);

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api")
            || context.User.Identity?.IsAuthenticated != true
            || !Applies(context.User)
            || IsAllowed(context))
        {
            await next(context);
            return;
        }

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = HoldTitle,
            Detail = HoldDetail,
            Extensions = { ["code"] = HoldCode }
        };

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        // Given to the write itself: it sets the content type, and would replace one set before it.
        await context.Response.WriteAsJsonAsync(problem, options: null, contentType: ProblemJson);
    }

    private bool IsAllowed(HttpContext context)
    {
        var request = context.Request;
        var path = request.Path.Value ?? string.Empty;

        if (HttpMethods.IsGet(request.Method)
            && (Is(path, "/api/auth/whoami") || Is(path, "/api/auth/me/settings") || Is(path, "/api/auth/features")))
            return true;

        // Signing in (as anyone, with either step) checks the credentials afresh and replaces this session, so it gives
        // nothing away; without it, a browser still holding a held session couldn't sign in again. Signing out ends it.
        if (HttpMethods.IsPost(request.Method)
            && (Is(path, "/api/auth/login")
                || Is(path, "/api/auth/login/two-factor")
                || Is(path, "/api/auth/logout")
                || Is(path, "/api/auth/password/forgot")
                || Is(path, "/api/auth/password/reset")
                || Is(path, "/api/auth/email/confirm")))
            return true;

        return AllowsForTask(context, path);
    }

    // Routing ignores case, so the allowlists do too.
    protected static bool Is(string value, string expected) =>
        string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
}
