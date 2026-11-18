namespace MedApp.Domain.Pathways.PatientPathways;

/// <summary>
/// One note on a pathway: Markdown text by one member of staff, newest first on the pathway page. Only its author
/// can edit it. Every add and edit writes a <see cref="PatientPathwayNoteVersion"/>, so its whole history is kept in a
/// table of its own; the audit log records only the note's ID and version.
/// <para>
/// An admin can delete a note, with a reason, directly or by approving a member of staff's
/// <see cref="PathwayNoteDeletionRequest"/>. Deleting is soft: the note is hidden from the pathway page for everyone,
/// but its row and versions stay, marked deleted, so the history and the patient's export still account for it.
/// </para>
/// </summary>
public sealed class PatientPathwayNote
{
    /// <summary>The longest a note's text can be.</summary>
    public const int MaxTextLength = 4000;

    /// <summary>The longest the detail beside a reason for deleting a note, or for asking to, can be.</summary>
    public const int MaxDeletionDetailLength = 500;

    public Guid Id { get; init; }

    public Guid PatientPathwayId { get; init; }
    public PatientPathway PatientPathway { get; set; } = null!;

    /// <summary>
    /// Who wrote it. Null only for a note moved from a pathway's old single notes field whose creator wasn't recorded
    /// (shown as "Unknown"); nobody can edit such a note.
    /// </summary>
    public Guid? AuthorUserId { get; init; }

    /// <summary>The text now, as Markdown: the latest version's.</summary>
    public string Text { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; init; }

    /// <summary>When it was last edited; null until it is.</summary>
    public DateTime? LastEditedAtUtc { get; set; }

    /// <summary>The number of its latest version: 1 when added, one more for each edit.</summary>
    public int Version { get; set; } = 1;

    /// <summary>When an admin deleted it (hidden from the pathway page, but kept); null while it isn't deleted.</summary>
    public DateTime? DeletedAtUtc { get; set; }

    /// <summary>The admin who deleted it.</summary>
    public Guid? DeletedByUserId { get; set; }

    /// <summary>Why it was deleted: a fixed choice. Null while it isn't deleted.</summary>
    public NoteDeletionReason? DeletionReason { get; set; }

    /// <summary>More about why (free text, at most <see cref="MaxDeletionDetailLength"/> characters), if the admin gave any.</summary>
    public string? DeletionDetail { get; set; }

    /// <summary>
    /// The member of staff's request to delete it that's waiting for an admin, if any: at most one at a time. Set and
    /// cleared with the request, so the note's row version also stops two requests being made at once.
    /// </summary>
    public Guid? PendingDeletionRequestId { get; set; }

    /// <summary>Whether an admin has deleted it.</summary>
    public bool IsDeleted => DeletedAtUtc is not null;

    /// <summary>A concurrency token: of two saves from the same copy, the later is refused (409) rather than overwriting.</summary>
    public byte[] RowVersion { get; set; } = [];
}
