namespace MedApp.Domain.Procedures.PatientProcedures;

/// <summary>
/// One note on a procedure: Markdown text by one member of staff, newest first on the procedure page. Only its author
/// can edit it, and nobody can delete it. Every add and edit writes a <see cref="PatientProcedureNoteVersion"/>, so its
/// whole history is kept in a table of its own; the audit log records only the note's ID and version.
/// </summary>
public sealed class PatientProcedureNote
{
    /// <summary>The longest a note's text can be.</summary>
    public const int MaxTextLength = 4000;

    public Guid Id { get; init; }

    public Guid PatientProcedureId { get; init; }
    public PatientProcedure PatientProcedure { get; set; } = null!;

    /// <summary>
    /// Who wrote it. Null only for a note moved from a procedure's old single notes field whose creator wasn't recorded
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

    /// <summary>A concurrency token: of two saves from the same copy, the later is refused (409) rather than overwriting.</summary>
    public byte[] RowVersion { get; set; } = [];
}
