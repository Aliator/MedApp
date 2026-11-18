namespace MedApp.Domain.Notifications;

/// <summary>
/// Which notifications a person has chosen to get, by their own choice in Account Settings. Someone with no row gets
/// every kind (the defaults). Kept apart from their app settings (which only the browser reads), because the server
/// checks it before sending. Monitoring notifications aren't here: an admin chooses those with their own rules.
/// </summary>
public sealed class NotificationPreferences
{
    public Guid UserId { get; init; }

    /// <summary>Whether to be told when someone assigns them to a pathway.</summary>
    public bool PathwayAssigned { get; set; } = true;

    public DateTime UpdatedAtUtc { get; set; }

    /// <summary>What someone who has never chosen gets: everything.</summary>
    public static NotificationPreferences Defaults(Guid userId) => new() { UserId = userId };

    /// <summary>Whether a person can turn this kind off (so it's worth looking up their preferences before sending).</summary>
    public static bool IsOptional(NotificationKind kind) => kind == NotificationKind.PathwayAssigned;

    /// <summary>Whether they want notifications of this kind.</summary>
    public bool Wants(NotificationKind kind) => kind switch
    {
        NotificationKind.PathwayAssigned => PathwayAssigned,
        _ => true
    };
}
