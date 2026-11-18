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
    NoteDeleted = 15,

    // 16 to 34 are kept for Phase 3's specimens, results and letters (docs/centre-phases-3-5-plan.md, section 5).

    /// <summary>
    /// A stage was skipped: an optional stage, by hand, with a reason (<c>skipReason</c> by name, and a <c>detail</c>
    /// for Other). Stages skipped because another branch was chosen are listed in that <see cref="BranchChosen"/> entry.
    /// </summary>
    StageSkipped = 35,

    /// <summary>
    /// Finishing a branch point chose one of its branches (<c>branch</c>); the other branches' stages after it were
    /// skipped (<c>skippedStageIds</c>, <c>skippedStages</c>). Written on the branch point, after its StageFinished.
    /// </summary>
    BranchChosen = 36,

    /// <summary>A skipped stage was reinstated, with a <c>reason</c> (free text); it runs again in its place.</summary>
    StageReinstated = 37,

    /// <summary>A pathway was listed for an MDT (the item's ID, the specialty and the meeting; never the question).</summary>
    MdtListed = 38,

    /// <summary>An MDT's outcome was recorded (the item's ID, the meeting and who was present; never the outcome).</summary>
    MdtOutcomeRecorded = 39,

    /// <summary>An MDT item was moved to a later meeting, or listed again after its meeting was cancelled.</summary>
    MdtDeferred = 40,

    /// <summary>An MDT item was taken off the list without being discussed.</summary>
    MdtWithdrawn = 41
}
