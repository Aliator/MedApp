namespace MedApp.Domain.Authentication;

public sealed class UserSession
{
    private UserSession()
    {
    }

    public UserSession(
        Guid userId,
        DateTime createdAtUtc,
        DateTime expiresAtUtc,
        string? createdByIp,
        string? userAgent)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        CreatedByIp = createdByIp;
        UserAgent = userAgent;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    public string? CreatedByIp { get; private set; }
    public string? UserAgent { get; private set; }

    public bool IsActive(DateTime utcNow) =>
        RevokedAtUtc is null && ExpiresAtUtc > utcNow;

    public void Revoke(DateTime utcNow)
    {
        if (RevokedAtUtc is null)
            RevokedAtUtc = utcNow;
    }
}