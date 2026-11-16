using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace MedApp.API.Common.RateLimiting;

/// <summary>
/// Throttles sign-in (<c>POST api/auth/login</c>) with ASP.NET Core's rate limiter, in front of Identity's lockout:
/// <list type="bullet">
/// <item>per client address: the <see cref="Policy"/> policy, which the rate limiting middleware applies before the
/// request reaches the controller;</item>
/// <item>per username, from anywhere: <see cref="ThrottleLoginByUsernameAttribute"/>, once the body has been read.</item>
/// </list>
/// Either refusal is a 429 <see cref="ProblemDetails"/> with <c>Retry-After</c> (seconds). Attempts are counted, right
/// or wrong, in fixed windows (<see cref="LoginThrottlingOptions"/>), in this copy of the API's memory.
/// </summary>
public static class LoginThrottling
{
    /// <summary>The rate limiting policy for sign-in, per client address.</summary>
    public const string Policy = "login";

    private const string ProblemJson = "application/problem+json";

    /// <summary>Registers the rate limiter, its sign-in policy, and the per-username limiter.</summary>
    public static IServiceCollection AddLoginThrottling(this IServiceCollection services)
    {
        services.AddOptions<LoginThrottlingOptions>().BindConfiguration(LoginThrottlingOptions.Section);
        services.AddSingleton<LoginUsernameLimiter>();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (context, ct) => new ValueTask(WriteTooManyAttemptsAsync(context.HttpContext, context.Lease, ct));
            options.AddPolicy(Policy, ClientPartition);
        });

        return services;
    }

    /// <summary>The sign-in policy's partition: one fixed window per client address.</summary>
    public static RateLimitPartition<string> ClientPartition(HttpContext context)
    {
        var settings = context.RequestServices.GetRequiredService<IOptions<LoginThrottlingOptions>>().Value;
        return RateLimitPartition.GetFixedWindowLimiter(
            ClientKey(context.Connection.RemoteIpAddress),
            _ => WindowOptions(settings.PerClientPermitLimit, settings.Window));
    }

    /// <summary>
    /// Who a client address counts as. Behind the ingress, it's the address from <c>X-Forwarded-For</c> (the host's
    /// forwarded headers, which the Helm chart turns on). An IPv6 address counts by its /64 network, which one client
    /// usually has the whole of, so it can't get a new allowance by changing address within it.
    /// </summary>
    public static string ClientKey(IPAddress? address)
    {
        if (address is null)
            return "unknown";

        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        if (address.AddressFamily != AddressFamily.InterNetworkV6)
            return address.ToString();

        var bytes = address.GetAddressBytes();
        Array.Clear(bytes, 8, 8);
        return new IPAddress(bytes) + "/64";
    }

    /// <summary>A fixed window with no queue: an attempt over the limit is refused at once.</summary>
    public static FixedWindowRateLimiterOptions WindowOptions(int permitLimit, TimeSpan window) => new()
    {
        PermitLimit = permitLimit,
        Window = window,
        QueueLimit = 0,
        AutoReplenishment = true,
    };

    /// <summary>How long a refused attempt should wait, in whole seconds: from the limiter, at least one.</summary>
    public static int RetryAfterSeconds(RateLimitLease lease, TimeSpan window)
    {
        var wait = lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter) ? retryAfter : window;
        return Math.Max(1, (int)Math.Ceiling(wait.TotalSeconds));
    }

    /// <summary>The refusal: what the sign-in page shows, in the same shape as the API's other errors.</summary>
    public static ProblemDetails TooManyAttempts(int retryAfterSeconds) => new()
    {
        Status = StatusCodes.Status429TooManyRequests,
        Title = $"Too many sign-in attempts. Try again in {retryAfterSeconds} {(retryAfterSeconds == 1 ? "second" : "seconds")}.",
    };

    /// <summary>Writes the refusal for <paramref name="lease"/>, with its <c>Retry-After</c>.</summary>
    public static async Task WriteTooManyAttemptsAsync(HttpContext context, RateLimitLease lease, CancellationToken ct)
    {
        var settings = context.RequestServices.GetRequiredService<IOptions<LoginThrottlingOptions>>().Value;
        var seconds = RetryAfterSeconds(lease, settings.Window);

        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
        await context.Response.WriteAsJsonAsync(TooManyAttempts(seconds), options: null, contentType: ProblemJson, ct);
    }
}
