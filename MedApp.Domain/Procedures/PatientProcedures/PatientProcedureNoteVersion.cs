namespace MedApp.Domain.Procedures.PatientProcedures;

/// <summary>
/// One version of a procedure's note: its text as added, or after one edit, with who and when. Like the audit log, it
/// has no foreign keys and copies in its patient and procedure, so a note's history outlives a deleted procedure, and a
/// patient's export and anonymisation find it by the patient.
/// </summary>
public sealed class PatientProcedureNoteVersion
{
    public Guid Id { get; init; }

    public Guid NoteId { get; init; }

    /// <summary>1 for the text as added, one more for each edit.</summary>
    public int Version { get; init; }

    public Guid PatientProcedureId { get; init; }

    public Guid PatientId { get; init; }

    /// <summary>The text of this version, as Markdown.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Who wrote this version; null only for a note moved from the old single field with no known creator.</summary>
    public Guid? UserId { get; init; }

    /// <summary>The username at the time, kept even if the account is later deleted (empty when unknown).</summary>
    public string Username { get; init; } = string.Empty;

    public DateTime AtUtc { get; init; }
}
