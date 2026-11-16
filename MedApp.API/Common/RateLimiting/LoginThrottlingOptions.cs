namespace MedApp.API.Common.RateLimiting;

/// <summary>
/// How many sign-in attempts (<c>POST api/auth/login</c>) are allowed in each <see cref="Window"/>, the
/// "LoginThrottling" configuration section (<see cref="LoginThrottling"/>). Counted by each copy of the API on its own.
/// </summary>
public sealed class LoginThrottlingOptions
{
    public const string Section = "LoginThrottling";

    /// <summary>
    /// Attempts from one client address. Generous, because a hospital's staff often share one public address (NAT),
    /// and everyone signs in at the start of a shift.
    /// </summary>
    public int PerClientPermitLimit { get; set; } = 30;

    /// <summary>
    /// Attempts at one username, from anywhere. As many as the lockout allows wrong passwords
    /// (<c>Identity:MaxFailedAccessAttempts</c>), so it slows guessing across many addresses without locking out
    /// anyone the lockout wouldn't.
    /// </summary>
    public int PerUsernamePermitLimit { get; set; } = 5;

    /// <summary>The window both limits count in. A fixed window: the count starts again when it ends.</summary>
    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Password reset requests (<c>POST api/auth/password/forgot</c>) for one username or email address, from anywhere,
    /// in each <see cref="ResetRequestWindow"/>: few, since each may send an email, so nobody can flood someone with reset
    /// emails. Counted whether or not an account matches, so the limit says nothing about which do.
    /// </summary>
    public int ResetRequestsPerIdentifierPermitLimit { get; set; } = 3;

    /// <summary>The window reset requests per username or address are counted in.</summary>
    public TimeSpan ResetRequestWindow { get; set; } = TimeSpan.FromMinutes(15);
}
