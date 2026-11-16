using System.Globalization;
using MedApp.Contracts.Authentication.Requests;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MedApp.API.Common.RateLimiting;

/// <summary>
/// Refuses a sign-in with a 429 (<see cref="LoginThrottling.TooManyAttempts"/>) once its username has had too many
/// attempts in the window (<see cref="LoginUsernameLimiter"/>). An action filter, because the username is in the body,
/// which the rate limiting middleware runs too early to read. A request without one is left to validation (a 400).
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ThrottleLoginByUsernameAttribute : ActionFilterAttribute
{
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var username = context.ActionArguments.Values.OfType<LoginRequest>().FirstOrDefault()?.Username;
        if (string.IsNullOrWhiteSpace(username))
        {
            await next();
            return;
        }

        var limiter = context.HttpContext.RequestServices.GetRequiredService<LoginUsernameLimiter>();
        using var lease = limiter.Attempt(username);
        if (lease.IsAcquired)
        {
            await next();
            return;
        }

        var seconds = LoginThrottling.RetryAfterSeconds(lease, limiter.Window);
        context.HttpContext.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
        context.Result = new ObjectResult(LoginThrottling.TooManyAttempts(seconds))
        {
            StatusCode = StatusCodes.Status429TooManyRequests,
            ContentTypes = { "application/problem+json" },
        };
    }
}
