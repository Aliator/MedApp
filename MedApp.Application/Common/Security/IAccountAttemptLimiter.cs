namespace MedApp.Application.Common.Security;

/// <summary>What an account is limited in doing, however many sign-ins or challenges it's spread across.</summary>
public enum AccountAttempt
{
    /// <summary>A two-factor code checked: at sign-in, or to turn two-factor off or replace the recovery codes.</summary>
    TwoFactorCode,

    /// <summary>A request to use a new email address, each of which sends an email.</summary>
    EmailChange,
}

/// <summary>
/// Limits what one account can try, from anywhere (the host's rate limiter, like sign-in's per-username limit). A
/// refused attempt throws <see cref="Exceptions.TooManyAttemptsException"/>, a 429 with <c>Retry-After</c>, before anything
/// is checked.
/// </summary>
public interface IAccountAttemptLimiter
{
    /// <summary>Counts an attempt; throws when the account has had too many of this kind in the window.</summary>
    void Attempt(AccountAttempt kind, Guid userId);
}

/// <summary>No limit: the default where the host doesn't provide one (tools that never take these requests).</summary>
public sealed class UnlimitedAccountAttempts : IAccountAttemptLimiter
{
    public void Attempt(AccountAttempt kind, Guid userId)
    {
    }
}
