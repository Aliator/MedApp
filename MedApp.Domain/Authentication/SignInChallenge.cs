namespace MedApp.Domain.Authentication;

/// <summary>
/// The second step of signing in to an account with two-factor authentication: the right password made one of these
/// instead of a session. It's short-lived (<see cref="Lifetime"/>), single-use (deleted when it's used), bound to its
/// user, and allows a few wrong codes (<see cref="MaxFailedAttempts"/>). Only a hash of its token is stored, so a copy
/// of the database can't be used to finish someone's sign-in.
/// </summary>
public sealed class SignInChallenge
{
    /// <summary>How long the second step can take with an authenticator app.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    /// <summary>
    /// How long the second step can take with a code by email: as long as the code itself works
    /// (<see cref="EmailCode.Lifetime"/>), since an email can take a minute or two to arrive.
    /// </summary>
    public static readonly TimeSpan EmailLifetime = EmailCode.Lifetime;

    /// <summary>Wrong codes allowed before the challenge stops working (each also counts towards the lockout).</summary>
    public const int MaxFailedAttempts = 5;

    // For EF Core.
    private SignInChallenge()
    {
    }

    /// <param name="lifetime">How long it lasts: <see cref="Lifetime"/> unless given (<see cref="EmailLifetime"/>).</param>
    public SignInChallenge(Guid userId, string tokenHash, DateTime createdAtUtc, TimeSpan? lifetime = null)
    {
        UserId = userId;
        TokenHash = tokenHash;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = createdAtUtc.Add(lifetime ?? Lifetime);
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }

    /// <summary>The SHA-256 hash of the challenge's token, in lower-case hex.</summary>
    public string TokenHash { get; private set; } = string.Empty;

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    /// <summary>
    /// Attempts used: each code checked reserves one first (<c>TryReserveAttemptAsync</c>, in one statement), right or
    /// wrong, so a right code on the last attempt still signs in.
    /// </summary>
    public int FailedAttempts { get; private set; }

    /// <summary>Whether it can still be used at <paramref name="utcNow"/>: not expired, and not out of attempts.</summary>
    public bool IsUsable(DateTime utcNow) =>
        ExpiresAtUtc > utcNow && FailedAttempts < MaxFailedAttempts;
}
