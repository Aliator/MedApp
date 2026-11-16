using System.Globalization;
using MedApp.Application.Common.Identity;
using MedApp.Contracts.Authentication.Requests;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace MedApp.API.Common.RateLimiting;

/// <summary>
/// Refuses a sign-in with a 429 (<see cref="LoginThrottling.TooManyAttempts"/>) once its username has had too many
/// attempts in the window (<see cref="LoginUsernameLimiter"/>). An action filter, because the username is in the body,
/// which the rate limiting middleware runs too early to read.
/// <para>
/// Only a name that could be an account's is counted (<see cref="UsernameRules.CouldExist"/>). None, one over
/// <see cref="UsernameRules.MaxLength"/> characters (validation refuses it, a 400), or one with characters no account's
/// name has (sign-in treats it as unknown, a 401, without looking it up) is left to those, so the limiter never keeps an
/// attacker's arbitrary string.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ThrottleLoginByUsernameAttribute : ActionFilterAttribute
{
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var services = context.HttpContext.RequestServices;
        var username = context.ActionArguments.Values.OfType<LoginRequest>().FirstOrDefault()?.Username;
        var allowed = services.GetService<IOptions<IdentityOptions>>()?.Value.User.AllowedUserNameCharacters;
        if (!UsernameRules.CouldExist(username, allowed))
        {
            await next();
            return;
        }

        var limiter = services.GetRequiredService<LoginUsernameLimiter>();
        using var lease = limiter.Attempt(username!);
        if (lease.IsAcquired)
        {
            await next();
            return;
        }

        var seconds = LoginThrottling.RetryAfterSeconds(lease, limiter.Window);
        LoginThrottling.LogRefusal(context.HttpContext, "per-username", limiter.PermitLimit, limiter.Window);
        context.HttpContext.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
        context.Result = new ObjectResult(LoginThrottling.TooManyAttempts(seconds))
        {
            StatusCode = StatusCodes.Status429TooManyRequests,
            ContentTypes = { "application/problem+json" },
        };
    }
}
