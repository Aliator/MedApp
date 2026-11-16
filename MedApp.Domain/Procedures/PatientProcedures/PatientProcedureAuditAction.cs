namespace MedApp.Domain.Procedures.PatientProcedures;

/// <summary>What an audit entry records. Stored as a number, so values never change.</summary>
public enum PatientProcedureAuditAction
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

    /// <summary>A file was attached to the patient, the procedure or one of its stages.</summary>
    AttachmentAdded = 10,

    /// <summary>An attached file was deleted (hidden; its bytes are purged later).</summary>
    AttachmentDeleted = 11,

    /// <summary>An attached file was downloaded. Not on the patient timeline: only in the audit log.</summary>
    AttachmentDownloaded = 12
}
