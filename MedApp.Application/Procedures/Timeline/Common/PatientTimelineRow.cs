using MedApp.Domain.Procedures.PatientProcedures;

namespace MedApp.Application.Procedures.Timeline.Common;

/// <summary>
/// An audit entry on the timeline, with its procedure's title now: null when the procedure has been deleted, or when
/// the entry is about the patient's own file rather than a procedure (<see cref="IsAboutPatient"/>).
/// </summary>
public sealed record PatientTimelineRow(PatientProcedureAuditEntry Entry, string? CurrentProcedureTitle)
{
    /// <summary>
    /// For a note added or edited (only for admins), the note's text in this version, from the note versions: the audit
    /// entry itself holds only the note's ID and version.
    /// </summary>
    public string? NoteText { get; init; }

    /// <summary>For a note edited (only for admins), the note's text in the version before.</summary>
    public string? PreviousNoteText { get; init; }

    /// <summary>About a file attached to the patient themselves, not to one of their procedures.</summary>
    public bool IsAboutPatient => Entry.PatientProcedureId == PatientProcedureAuditEntry.NoProcedure;
}
