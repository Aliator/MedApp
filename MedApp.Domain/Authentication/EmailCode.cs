namespace MedApp.Domain.Authentication;

/// <summary>What an emailed two-factor code is for. Stored as a number, so values never change.</summary>
public enum EmailCodePurpose
{
    /// <summary>The second step of signing in: bound to one sign-in challenge, deleted with it.</summary>
    SignIn = 0,

    /// <summary>Turning codes by email on: proves the mailbox is the account holder's.</summary>
    TurnOn = 1,

    /// <summary>Confirming a change to two-factor while it's on with email codes (turning it off, new recovery codes).</summary>
    Confirm = 2,
}

/// <summary>
/// A two-factor code MedApp emailed to an account's confirmed address. Six digits from a cryptographic random number
/// generator, valid for <see cref="Lifetime"/>, used once (deleted when it's accepted), and stored only as a hash bound to
/// this row (<c>EmailCodes.Hash</c>), so it can't be read from the database or a log. Each account has at most one live
/// code for each purpose (and for sign-in, one per challenge): a new one replaces the last. It can be sent again
/// (<see cref="MaxResends"/> times, no more than once each <see cref="ResendInterval"/>), each time as a new code.
/// </summary>
public sealed class EmailCode
{
    /// <summary>How long a code works after it's sent.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    /// <summary>The shortest wait before the code can be sent again.</summary>
    public static readonly TimeSpan ResendInterval = TimeSpan.FromMinutes(1);

    /// <summary>How many times a code can be sent again, after the first.</summary>
    public const int MaxResends = 3;

    /// <summary>
    /// Wrong codes allowed before a code for turning email codes on or confirming a change stops working. A sign-in code
    /// is limited by its challenge's attempts instead (<see cref="SignInChallenge.MaxFailedAttempts"/>).
    /// </summary>
    public const int MaxFailedAttempts = 5;

    // For EF Core.
    private EmailCode()
    {
    }

    /// <summary>A new code's row, not yet holding a code: <see cref="Issue"/> sets it, once its hash is known.</summary>
    /// <param name="notAfterUtc">
    /// When what it's for ends, if sooner than <see cref="Lifetime"/> (a sign-in's challenge): the code ends then too.
    /// </param>
    public EmailCode(Guid userId, EmailCodePurpose purpose, Guid? challengeId, DateTime createdAtUtc, DateTime? notAfterUtc = null)
    {
        UserId = userId;
        Purpose = purpose;
        ChallengeId = challengeId;
        SentAtUtc = createdAtUtc;
        ExpiresAtUtc = ExpiryFor(createdAtUtc, notAfterUtc);
    }

    /// <summary>When a code sent at <paramref name="sentAtUtc"/> stops working: <see cref="Lifetime"/> on, or sooner.</summary>
    public static DateTime ExpiryFor(DateTime sentAtUtc, DateTime? notAfterUtc)
    {
        var expires = sentAtUtc.Add(Lifetime);
        return notAfterUtc is { } end && end < expires ? end : expires;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public EmailCodePurpose Purpose { get; private set; }

    /// <summary>The sign-in challenge a sign-in code belongs to; null for the other purposes.</summary>
    public Guid? ChallengeId { get; private set; }

    /// <summary>The hash of the code (<c>EmailCodes.Hash</c>), in lower-case hex.</summary>
    public string CodeHash { get; private set; } = string.Empty;

    /// <summary>When the code now in use was sent.</summary>
    public DateTime SentAtUtc { get; private set; }

    public DateTime ExpiresAtUtc { get; private set; }

    /// <summary>How many times it has been sent again.</summary>
    public int Resends { get; private set; }

    /// <summary>Wrong codes tried (turning on and confirming only), each reserved before it's checked.</summary>
    public int FailedAttempts { get; private set; }

    /// <summary>Sets the code (as its hash) of a new row.</summary>
    public void Issue(string codeHash) => CodeHash = codeHash;

    /// <summary>Whether it still works at <paramref name="utcNow"/>: not expired, and with tries left.</summary>
    public bool IsLive(DateTime utcNow) => ExpiresAtUtc > utcNow && FailedAttempts < MaxFailedAttempts;

    /// <summary>When it can next be sent again (by the server's clock).</summary>
    public DateTime ResendAvailableAtUtc => SentAtUtc.Add(ResendInterval);

    public int ResendsLeft => Math.Max(0, MaxResends - Resends);
}
