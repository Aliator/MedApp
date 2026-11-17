namespace MedApp.Contracts.Procedures.PatientProcedures.Responses;

/// <summary>One note on a procedure, as its page shows it.</summary>
public sealed class PatientProcedureNoteResponse
{
    public Guid Id { get; set; }

    /// <summary>Who wrote it; null when that wasn't recorded (a note moved from the old single notes field).</summary>
    public Guid? AuthorUserId { get; set; }

    /// <summary>The author's username, "Deleted user" once their account is gone, or "Unknown" when not recorded.</summary>
    public string AuthorUsername { get; set; } = string.Empty;

    /// <summary>The text now, as Markdown.</summary>
    public string Text { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    /// <summary>When it was last edited; null if it never has been.</summary>
    public DateTime? LastEditedAtUtc { get; set; }

    /// <summary>Its latest version (1 when added); send it back as <c>expectedVersion</c> to edit it.</summary>
    public int Version { get; set; }

    /// <summary>Whether the signed-in user can edit it now: only its author, while the procedure's notes can change.</summary>
    public bool CanEdit { get; set; }
}
