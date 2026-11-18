namespace MedApp.Domain.Pathways.PatientPathways;

/// <summary>What an audit entry records. Stored as a number, so values never change.</summary>
public enum PatientPathwayAuditAction
{
    Created = 0,
    DetailsUpdated = 1,
    StatusChanged = 2,
    Assigned = 3,
    Unassigned = 4,
    StageStarted = 5,
    StageNotesUpdated = 6,
    StageFinished = 7,
    StageUnlocked = 8,
    Deleted = 9,

    /// <summary>A file was attached to the patient, the pathway or one of its stages.</summary>
    AttachmentAdded = 10,

    /// <summary>An attached file was deleted (hidden; its bytes are purged later).</summary>
    AttachmentDeleted = 11,

    /// <summary>An attached file was downloaded. Not on the patient timeline: only in the audit log.</summary>
    AttachmentDownloaded = 12,

    /// <summary>A note was added to the pathway. The details hold its ID and version, never its text.</summary>
    NoteAdded = 13,

    /// <summary>A note on the pathway was edited. The details hold its ID and new version, never its text.</summary>
    NoteEdited = 14,

    /// <summary>
    /// An admin deleted a note on the pathway (soft: it's hidden, but kept). The details hold its ID and latest version,
    /// the admin's reason, and the request it approved, if any; never its text.
    /// </summary>
    NoteDeleted = 15
}
