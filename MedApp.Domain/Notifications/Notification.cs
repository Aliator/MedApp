namespace MedApp.Domain.Notifications;

/// <summary>
/// Something that happened that a user should know about, e.g. being assigned to a procedure. Kept, so it's there
/// after signing in again, and marked read once they've seen it. Like the audit log, it has no foreign keys, so it
/// outlives the procedure it's about.
/// </summary>
public sealed class Notification
{
    public Guid Id { get; init; }

    /// <summary>Who it's for.</summary>
    public Guid UserId { get; init; }

    public NotificationKind Kind { get; init; }

    /// <summary>One line, e.g. the procedure's title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>What happened, e.g. "admin assigned you to it."</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>The procedure it's about, if any, for a link to it.</summary>
    public Guid? ProcedureId { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    /// <summary>When the user marked it read; null while unread.</summary>
    public DateTime? ReadAtUtc { get; set; }
}
