using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace MedApp.API.Common.Authentication;

/// <summary>
/// Holds an admin who must use two-factor authentication but hasn't set up an authenticator app (their session's
/// <see cref="SessionAuthenticationDefaults.MustSetUpTwoFactorClaim"/>: admins are required to use it, in Admin settings,
/// and the requirement needs the app, so an admin with codes by email is held too) to setting the app up (an
/// <see cref="AccountHold"/>): every other <c>/api</c> request is refused with a 403
/// <see cref="ProblemDetails"/> whose <c>code</c> is <see cref="Code"/>, and the web app sends their pages to its set-up
/// page. Beyond what every hold allows, they may read their two-factor state, get a new authenticator key, and turn it
/// on with a code (which, for an admin with codes by email, replaces them, after an emailed code proves the current
/// factor: they may ask for one). A user who must also change their password is held by <see cref="PasswordChangeRequired"/> first.
/// </summary>
public sealed class TwoFactorSetupRequired(RequestDelegate next) : AccountHold(next)
{
    public const string Title = "Two-factor authentication required";
    public const string Code = "two_factor_setup_required";

    protected override string HoldTitle => Title;
    protected override string HoldCode => Code;
    protected override string HoldDetail => "Set up two-factor authentication before doing anything else.";

    protected override bool Applies(ClaimsPrincipal user) =>
        user.MustSetUpTwoFactor() && !user.MustChangePassword();

    protected override bool AllowsForTask(HttpContext context, string path)
    {
        var method = context.Request.Method;
        if (HttpMethods.IsGet(method))
            return Is(path, "/api/auth/me/two-factor");

        return HttpMethods.IsPost(method)
               && (Is(path, "/api/auth/me/two-factor/authenticator")
                   || Is(path, "/api/auth/me/two-factor/enable")
                   // An admin with codes by email proves them with an emailed code to switch to the app.
                   || Is(path, "/api/auth/me/two-factor/email/code"));
    }
}
