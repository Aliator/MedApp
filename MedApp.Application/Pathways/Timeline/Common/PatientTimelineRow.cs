using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.Application.Pathways.Timeline.Common;

/// <summary>
/// An audit entry on the timeline, with its pathway's title now: null when the pathway has been deleted, or when
/// the entry is about the patient's own file rather than a pathway (<see cref="IsAboutPatient"/>).
/// </summary>
public sealed record PatientTimelineRow(PatientPathwayAuditEntry Entry, string? CurrentPathwayTitle)
{
    /// <summary>
    /// For a note added or edited (only for admins), the note's text in this version, from the note versions: the audit
    /// entry itself holds only the note's ID and version.
    /// </summary>
    public string? NoteText { get; init; }

    /// <summary>For a note edited (only for admins), the note's text in the version before.</summary>
    public string? PreviousNoteText { get; init; }

    /// <summary>
    /// For a letter entry (only for admins), the letter as it is now, for its free text (the return comment and the
    /// withdrawal reason): the audit entry itself holds none.
    /// </summary>
    public Domain.Letters.Letter? Letter { get; init; }

    /// <summary>About a file attached to the patient themselves, not to one of their pathways.</summary>
    public bool IsAboutPatient => Entry.PatientPathwayId == PatientPathwayAuditEntry.NoPathway;
}
