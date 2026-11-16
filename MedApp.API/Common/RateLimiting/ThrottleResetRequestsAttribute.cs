using System.Globalization;
using MedApp.Application.Common.Identity;
using MedApp.Contracts.Authentication.Requests;
using MedApp.Infrastructure.Common.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace MedApp.API.Common.RateLimiting;

/// <summary>
/// Refuses a password reset request with a 429 once its username or address has had too many in the window
/// (<see cref="ResetRequestLimiter"/>), beside the per-address sign-in policy. An action filter, because the identifier is
/// in the body. Only something that could be a username (<see cref="UsernameRules.CouldExist"/>) or an address
/// (<see cref="PasswordResetService.CouldBeEmail"/>) is counted, so the limiter never keeps an arbitrary string; the rest
/// matches no account anyway.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ThrottleResetRequestsAttribute : ActionFilterAttribute
{
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var services = context.HttpContext.RequestServices;
        var identifier = context.ActionArguments.Values.OfType<ForgotPasswordRequest>().FirstOrDefault()?.UsernameOrEmail?.Trim();
        var allowed = services.GetService<IOptions<IdentityOptions>>()?.Value.User.AllowedUserNameCharacters;
        if (string.IsNullOrEmpty(identifier)
            || !(UsernameRules.CouldExist(identifier, allowed) || PasswordResetService.CouldBeEmail(identifier)))
        {
            await next();
            return;
        }

        var limiter = services.GetRequiredService<ResetRequestLimiter>();
        using var lease = limiter.Attempt(identifier);
        if (lease.IsAcquired)
        {
            await next();
            return;
        }

        var seconds = LoginThrottling.RetryAfterSeconds(lease, limiter.Window);
        LoginThrottling.LogRefusal(context.HttpContext, "per-identifier reset request", limiter.PermitLimit, limiter.Window);
        context.HttpContext.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
        context.Result = new ObjectResult(TooManyRequests(seconds))
        {
            StatusCode = StatusCodes.Status429TooManyRequests,
            ContentTypes = { "application/problem+json" },
        };
    }

    /// <summary>The refusal, in minutes when it's that long (as the reset window usually is).</summary>
    public static ProblemDetails TooManyRequests(int retryAfterSeconds)
    {
        var wait = retryAfterSeconds >= 60
            ? $"{(int)Math.Ceiling(retryAfterSeconds / 60.0)} {(retryAfterSeconds <= 60 ? "minute" : "minutes")}"
            : $"{retryAfterSeconds} {(retryAfterSeconds == 1 ? "second" : "seconds")}";

        return new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = $"Too many reset requests for this name or address. Try again in {wait}.",
        };
    }
}
