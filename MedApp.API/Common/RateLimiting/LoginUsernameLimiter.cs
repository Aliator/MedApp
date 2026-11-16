using System.Text;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;

namespace MedApp.API.Common.RateLimiting;

/// <summary>
/// Counts sign-in attempts per username, from any address (<see cref="LoginThrottlingOptions.PerUsernamePermitLimit"/>
/// in each window), so guessing one account's password from many addresses is as slow as from one. The filter only
/// counts names that could be an account's (<see cref="ThrottleLoginByUsernameAttribute"/>); this keys them trimmed,
/// in Unicode compatibility form (NFKC, so a look-alike form of a letter counts as the letter) and ignoring case, as the
/// database compares them.
/// </summary>
public sealed class LoginUsernameLimiter : IDisposable
{
    private readonly PartitionedRateLimiter<string> _limiter;

    public LoginUsernameLimiter(IOptions<LoginThrottlingOptions> options)
    {
        var settings = options.Value;
        Window = settings.Window;
        PermitLimit = settings.PerUsernamePermitLimit;
        _limiter = PartitionedRateLimiter.Create<string, string>(username =>
            RateLimitPartition.GetFixedWindowLimiter(
                username,
                _ => LoginThrottling.WindowOptions(settings.PerUsernamePermitLimit, settings.Window)));
    }

    /// <summary>The window attempts are counted in.</summary>
    public TimeSpan Window { get; }

    /// <summary>Attempts allowed per username in each window.</summary>
    public int PermitLimit { get; }

    /// <summary>The key an attempt at <paramref name="username"/> is counted under.</summary>
    public static string Key(string username) =>
        username.Trim().Normalize(NormalizationForm.FormKC).ToUpperInvariant();

    /// <summary>Counts an attempt at <paramref name="username"/>; the lease says whether it's allowed.</summary>
    public RateLimitLease Attempt(string username) => _limiter.AttemptAcquire(Key(username));

    public void Dispose() => _limiter.Dispose();
}
