namespace MedApp.Domain.Authentication;

/// <summary>
/// A user's app settings (colours, text size, list defaults, favourites and the rest), kept with the account so
/// they're the same on every computer: one JSON object, which the app reads and writes whole.
/// </summary>
public sealed class UserSettings(Guid userId, string json, DateTime updatedAtUtc)
{
    public Guid UserId { get; private set; } = userId;
    public string Json { get; private set; } = json;
    public DateTime UpdatedAtUtc { get; private set; } = updatedAtUtc;

    public void Replace(string json, DateTime utcNow)
    {
        Json = json;
        UpdatedAtUtc = utcNow;
    }
}
