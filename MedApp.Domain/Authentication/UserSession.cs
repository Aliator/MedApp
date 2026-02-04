namespace MedApp.Domain.Authentication;

public sealed class UserSession(
    Guid userId,
    DateTime createdAtUtc,
    DateTime expiresAtUtc,
    string? createdByIp,
    string? userAgent)
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; } = userId;
    public DateTime CreatedAtUtc { get; private set; } = createdAtUtc;
    public DateTime ExpiresAtUtc { get; private set; } = expiresAtUtc;
    public DateTime? RevokedAtUtc { get; private set; }
    public string? CreatedByIp { get; private set; } = createdByIp;
    public string? UserAgent { get; private set; } = userAgent;

    public bool IsActive(DateTime utcNow) =>
        RevokedAtUtc is null && ExpiresAtUtc > utcNow;

    public void Revoke(DateTime utcNow)
    {
        if (RevokedAtUtc is null)
            RevokedAtUtc = utcNow;
    }
}