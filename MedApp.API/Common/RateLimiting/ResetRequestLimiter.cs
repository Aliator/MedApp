using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;

namespace MedApp.API.Common.RateLimiting;

/// <summary>
/// Counts password reset requests per username or email address, from any client address
/// (<see cref="LoginThrottlingOptions.ResetRequestsPerIdentifierPermitLimit"/> in each
/// <see cref="LoginThrottlingOptions.ResetRequestWindow"/>), keyed as sign-in keys usernames
/// (<see cref="LoginUsernameLimiter.Key"/>), so one person can't be sent a flood of reset emails.
/// </summary>
public sealed class ResetRequestLimiter : IDisposable
{
    private readonly PartitionedRateLimiter<string> _limiter;

    public ResetRequestLimiter(IOptions<LoginThrottlingOptions> options)
    {
        var settings = options.Value;
        Window = settings.ResetRequestWindow;
        PermitLimit = settings.ResetRequestsPerIdentifierPermitLimit;
        _limiter = PartitionedRateLimiter.Create<string, string>(identifier =>
            RateLimitPartition.GetFixedWindowLimiter(
                identifier,
                _ => LoginThrottling.WindowOptions(settings.ResetRequestsPerIdentifierPermitLimit, settings.ResetRequestWindow)));
    }

    public TimeSpan Window { get; }

    public int PermitLimit { get; }

    /// <summary>Counts a request for <paramref name="identifier"/>; the lease says whether it's allowed.</summary>
    public RateLimitLease Attempt(string identifier) => _limiter.AttemptAcquire(LoginUsernameLimiter.Key(identifier));

    public void Dispose() => _limiter.Dispose();
}
