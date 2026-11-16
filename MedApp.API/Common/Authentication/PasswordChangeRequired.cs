using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace MedApp.API.Common.Authentication;

/// <summary>
/// Holds a user who must change the password an admin gave them (their session's
/// <see cref="SessionAuthenticationDefaults.MustChangePasswordClaim"/>) to that one task (an <see cref="AccountHold"/>):
/// every other <c>/api</c> request, the notifications hub included, is refused with a 403 <see cref="ProblemDetails"/>
/// whose <c>code</c> is <see cref="Code"/>. Beyond what every hold allows (asking who they are, their own UI settings,
/// signing in again and out), they may change their own password. Pages and static files pass, because the web app sends
/// them to its change-password page. It comes before <see cref="TwoFactorSetupRequired"/>: the password first.
/// </summary>
public sealed class PasswordChangeRequired(RequestDelegate next) : AccountHold(next)
{
    public const string Title = "Password change required";
    public const string Code = "password_change_required";

    protected override string HoldTitle => Title;
    protected override string HoldCode => Code;
    protected override string HoldDetail => "Choose your own password before doing anything else.";

    protected override bool Applies(ClaimsPrincipal user) => user.MustChangePassword();

    protected override bool AllowsForTask(HttpContext context, string path) =>
        HttpMethods.IsPut(context.Request.Method) && IsOwnPasswordChange(context, path);

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
}
