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

    // Specimens (docs/centre-phases-3-5-plan.md, section 5). Not changes to the pathway: they don't go through
    // PathwayChange and don't move its LastUpdated. Details hold the specimen's ID and number, its type and lab by ID and
    // name, statuses and fixed choices by name; never its site, request notes or any detail typed (SpecimenAudit).

    /// <summary>A specimen was taken at a stage.</summary>
    SpecimenTaken = 16,

    /// <summary>A specimen was sent to the lab.</summary>
    SpecimenSent = 17,

    /// <summary>The lab received a specimen.</summary>
    SpecimenReceived = 18,

    /// <summary>A specimen's report was attached (the file's ID in the details).</summary>
    SpecimenReported = 19,

    /// <summary>The lab rejected a specimen: the reason by name, never its detail.</summary>
    SpecimenRejected = 20,

    /// <summary>A specimen was marked lost.</summary>
    SpecimenLost = 21,

    /// <summary>A specimen was marked entered in error.</summary>
    SpecimenEnteredInError = 22,

    /// <summary>A specimen's details were edited before it was sent: the names of the fields changed, values only for structured ones.</summary>
    SpecimenDetailsEdited = 23,

    // 24 to 28 are kept for results (docs/centre-phases-3-5-plan.md, section 5).

    /// <summary>
    /// A letter was started (a draft, from a template). Letters' entries (29 to 34) hold the letter's ID, kind and
    /// template name, recipients' kinds and methods, and dates; never its text, a comment or a reason (decision 70).
    /// </summary>
    LetterDrafted = 29,

    /// <summary>A draft was submitted to a named approver (by username).</summary>
    LetterSubmitted = 30,

    /// <summary>The approver returned it to its author with a comment (the comment only on the letter).</summary>
    LetterReturned = 31,

    /// <summary>It was approved: its PDF made and kept as a file (the file's ID in the details).</summary>
    LetterApproved = 32,

    /// <summary>It was sent to one recipient (the recipient's kind and the method), or a secure email to one failed.</summary>
    LetterSent = 33,

    /// <summary>An approved letter was withdrawn (its reason only on the letter), or a draft discarded.</summary>
    LetterWithdrawn = 34,

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

    // 38 to 41 are kept for the MDT list (docs/centre-phases-3-5-plan.md, section 5).

    /// <summary>
    /// Consent to a stage was recorded. Consent entries (42 to 44) hold the record's ID, the form's name and version
    /// (admin-written), who signed by name, the date signed and whether there's a scan; never a detail typed (decision 70).
    /// </summary>
    ConsentRecorded = 42,

    /// <summary>Consent was withdrawn, on a date (the detail only on the record).</summary>
    ConsentWithdrawn = 43,

    /// <summary>A consent record was marked entered in error (the detail only on the record).</summary>
    ConsentEnteredInError = 44
}
