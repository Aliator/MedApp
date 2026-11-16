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
}
