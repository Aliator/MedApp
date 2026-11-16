using System.Threading.RateLimiting;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Security;
using Microsoft.Extensions.Options;

namespace MedApp.API.Common.RateLimiting;

/// <summary>
/// Counts each account's two-factor codes and email changes in fixed windows, from any address
/// (<see cref="LoginThrottlingOptions.TwoFactorCodesPerAccountPermitLimit"/>,
/// <see cref="LoginThrottlingOptions.EmailChangesPerAccountPermitLimit"/>), in this copy of the API's memory, as sign-in's
/// per-username limit is. A code is counted before it's checked, right or wrong, so however the guesses are spread over
/// sign-ins, challenges and signed-in checks, an account gets at most this many in a window.
/// </summary>
public sealed class AccountAttemptLimiter : IAccountAttemptLimiter, IDisposable
{
    private readonly PartitionedRateLimiter<string> _codes;
    private readonly PartitionedRateLimiter<string> _emailChanges;
    private readonly LoginThrottlingOptions _settings;

    public AccountAttemptLimiter(IOptions<LoginThrottlingOptions> options)
    {
        _settings = options.Value;
        _codes = Create(_settings.TwoFactorCodesPerAccountPermitLimit, _settings.TwoFactorCodeWindow);
        _emailChanges = Create(_settings.EmailChangesPerAccountPermitLimit, _settings.EmailChangeWindow);
    }

    public void Attempt(AccountAttempt kind, Guid userId)
    {
        var (limiter, window, message) = kind switch
        {
            AccountAttempt.TwoFactorCode => (_codes, _settings.TwoFactorCodeWindow, "Too many codes tried for this account."),
            AccountAttempt.EmailChange => (_emailChanges, _settings.EmailChangeWindow, "Too many email address changes for this account."),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "No limit for this kind of attempt."),
        };

        using var lease = limiter.AttemptAcquire(userId.ToString("N"));
        if (lease.IsAcquired)
            return;

        var seconds = LoginThrottling.RetryAfterSeconds(lease, window);
        throw new TooManyAttemptsException($"{message} Try again in {Wait(seconds)}.", seconds);
    }

    /// <summary>A wait in words, in minutes once it's a minute or more.</summary>
    public static string Wait(int seconds) =>
        seconds >= 60
            ? $"{(int)Math.Ceiling(seconds / 60.0)} {(seconds <= 60 ? "minute" : "minutes")}"
            : $"{seconds} {(seconds == 1 ? "second" : "seconds")}";

    public void Dispose()
    {
        _codes.Dispose();
        _emailChanges.Dispose();
    }

    private static PartitionedRateLimiter<string> Create(int permitLimit, TimeSpan window) =>
        PartitionedRateLimiter.Create<string, string>(key =>
            RateLimitPartition.GetFixedWindowLimiter(key, _ => LoginThrottling.WindowOptions(permitLimit, window)));
}
