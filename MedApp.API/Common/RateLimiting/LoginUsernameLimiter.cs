using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;

namespace MedApp.API.Common.RateLimiting;

/// <summary>
/// Counts sign-in attempts per username, from any address (<see cref="LoginThrottlingOptions.PerUsernamePermitLimit"/>
/// in each window), so guessing one account's password from many addresses is as slow as from one. Usernames are
/// compared as Identity compares them (ignoring case), and trimmed.
/// </summary>
public sealed class LoginUsernameLimiter : IDisposable
{
    private readonly PartitionedRateLimiter<string> _limiter;

    public LoginUsernameLimiter(IOptions<LoginThrottlingOptions> options)
    {
        var settings = options.Value;
        Window = settings.Window;
        _limiter = PartitionedRateLimiter.Create<string, string>(username =>
            RateLimitPartition.GetFixedWindowLimiter(
                username,
                _ => LoginThrottling.WindowOptions(settings.PerUsernamePermitLimit, settings.Window)));
    }

    /// <summary>The window attempts are counted in.</summary>
    public TimeSpan Window { get; }

    /// <summary>Counts an attempt at <paramref name="username"/>; the lease says whether it's allowed.</summary>
    public RateLimitLease Attempt(string username) =>
        _limiter.AttemptAcquire(username.Trim().ToUpperInvariant());

    public void Dispose() => _limiter.Dispose();
}
