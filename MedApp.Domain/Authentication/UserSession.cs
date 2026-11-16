namespace MedApp.Domain.Authentication;

public sealed class UserSession(
    Guid userId,
    DateTime createdAtUtc,
    DateTime expiresAtUtc,
    string? createdByIp,
    string? userAgent)
{
    /// <summary>How long a session lasts from sign-in, and from each time it's extended.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(8);

    /// <summary>
    /// The longest a session can last from sign-in, however often it's extended. Over 20 hours, so this hard limit
    /// is one WCAG 2.2.1 allows; within it, the 8-hour limit can be extended as often as wanted.
    /// </summary>
    public static readonly TimeSpan MaxLifetime = TimeSpan.FromHours(24);

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; } = userId;
    public DateTime CreatedAtUtc { get; private set; } = createdAtUtc;
    public DateTime ExpiresAtUtc { get; private set; } = expiresAtUtc;
    public DateTime? RevokedAtUtc { get; private set; }
    public string? CreatedByIp { get; private set; } = createdByIp;
    public string? UserAgent { get; private set; } = userAgent;

    /// <summary>The latest it can be extended to: <see cref="MaxLifetime"/> after sign-in.</summary>
    public DateTime LatestExpiryUtc => CreatedAtUtc.Add(MaxLifetime);

    public bool IsActive(DateTime utcNow) =>
        RevokedAtUtc is null && ExpiresAtUtc > utcNow;

    /// <summary>Whether extending it now would move its expiry: it's active and hasn't reached its latest.</summary>
    public bool CanExtend(DateTime utcNow) =>
        IsActive(utcNow) && ExpiresAtUtc < LatestExpiryUtc;

    /// <summary>
    /// Extends it to a full <see cref="Lifetime"/> from now, but never past <see cref="LatestExpiryUtc"/>, and never
    /// earlier than it already ends. A session that has ended stays ended.
    /// </summary>
    public void Extend(DateTime utcNow)
    {
        if (!IsActive(utcNow))
            return;

        var extended = utcNow.Add(Lifetime);
        if (extended > LatestExpiryUtc)
            extended = LatestExpiryUtc;

        if (extended > ExpiresAtUtc)
            ExpiresAtUtc = extended;
    }

    public void Revoke(DateTime utcNow)
    {
        RevokedAtUtc ??= utcNow;
    }
}
