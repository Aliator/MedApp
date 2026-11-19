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

    // Results (docs/centre-phases-3-5-plan.md, section 5). Not changes to the pathway either. Details hold the result's
    // ID, source, the specimen's number and type, statuses, the action by name and usernames; never its title, action
    // detail or error detail (ResultChange).

    /// <summary>A result was made: from a specimen's report, or an attached file marked as an external result.</summary>
    ResultRecorded = 24,

    /// <summary>A clinician marked a result seen.</summary>
    ResultSeen = 25,

    /// <summary>A result was signed off: the action by name, never its detail.</summary>
    ResultSignedOff = 26,

    /// <summary>A result was given to another responsible clinician (taken over, or reassigned): the usernames.</summary>
    ResultReassigned = 27,

    /// <summary>A result was marked entered in error: its file deleted, a specimen's back to Received.</summary>
    ResultEnteredInError = 28,

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
    /// A choice node's outcome was recorded (MC10b): <c>{outcome, stageIds}</c>, the stages it added. Written on the choice
    /// node, after its StageFinished. Before choice nodes, a branch point's <c>branch</c>, with the other branches'
    /// stages skipped (<c>skippedStageIds</c>, <c>skippedStages</c>).
    /// </summary>
    BranchChosen = 36,

    /// <summary>A skipped stage was reinstated, with a <c>reason</c> (free text); it runs again in its place.</summary>
    StageReinstated = 37,

    /// <summary>A pathway was listed for an MDT (the item's ID, the team and the meeting; never the question).</summary>
    MdtListed = 38,

    /// <summary>An MDT's outcome was recorded (the item's ID, the meeting and who was present; never the outcome).</summary>
    MdtOutcomeRecorded = 39,

    /// <summary>An MDT item was moved to a later meeting, or listed again after its meeting was cancelled.</summary>
    MdtDeferred = 40,

    /// <summary>An MDT item was taken off the list without being discussed.</summary>
    MdtWithdrawn = 41,

    // 38 to 41 are kept for the MDT list (docs/centre-phases-3-5-plan.md, section 5).

    /// <summary>
    /// Consent to a stage was recorded. Consent entries (42 to 44) hold the record's ID, the form's name and version
    /// (admin-written), who signed by name, the date signed and whether there's a scan; never a detail typed (decision 70).
    /// </summary>
    ConsentRecorded = 42,

    /// <summary>Consent was withdrawn, on a date (the detail only on the record).</summary>
    ConsentWithdrawn = 43,

    /// <summary>A consent record was marked entered in error (the detail only on the record).</summary>
    ConsentEnteredInError = 44,

    // 45 and 46 are reserved by the earlier plan.

    /// <summary>
    /// A stage another team works became current, handing the pathway over (MC7, the relay): <c>{stageId, fromTeamId,
    /// from, toTeamId, to}</c> (the teams' admin-written names). Written on the stage reached.
    /// </summary>
    StageHandedOver = 47,

    /// <summary>A handed-over stage was picked up by its team (Pick Up, or starting or finishing it).</summary>
    StagePickedUp = 48,

    /// <summary>
    /// A template's addable branch was added (MC10): <c>{templateBranchId, branch, afterStageId, stageIds}</c>; never
    /// the reason, which is kept on the branch addition.
    /// </summary>
    BranchAdded = 49,

    // Lab cases (MC15): written by LabCaseChange, on the case's pathway (or none: PatientPathwayAuditEntry.NoPathway).
    // Details hold the case's ID and number and the test, steps and outcomes by ID and name; never the results, a
    // detail typed or why a repeat was asked for.

    /// <summary>A lab case was opened: <c>{labCaseId, number, labTestId, test, source}</c>.</summary>
    LabCaseOpened = 50,

    /// <summary>A step or a choice's outcome was recorded: <c>{labCaseId, number, stepId, step, choice, outcome}</c>.</summary>
    LabStepRecorded = 51,

    /// <summary>The latest step was marked entered in error: <c>{labCaseId, number, stepId, step}</c>.</summary>
    LabStepEnteredInError = 52,

    /// <summary>The results were entered and the report authorised: <c>{labCaseId, number, resultId}</c>.</summary>
    LabReportAuthorised = 53,

    /// <summary>The sample was insufficient and a repeat asked for: <c>{labCaseId, number}</c>.</summary>
    RepeatSampleRequested = 54,

    /// <summary>The case was marked entered in error, freeing its samples: <c>{labCaseId, number}</c>.</summary>
    LabCaseEnteredInError = 55,

    /// <summary>
    /// A hospital case's authorised report was sent back to its hospital (MC20), recorded as sent: <c>{labCaseId,
    /// number, hospitalId, hospital, letterKind, recipientKind, method}</c>. Never the surgeon's name or the results.
    /// </summary>
    LabReportSent = 56,

    /// <summary>The hospital's surgery details were added (MC20): <c>{labCaseId, number, surgeryDetailsId, noSurgeryExpected}</c>; never the text.</summary>
    SurgeryDetailsAdded = 57,

    /// <summary>A hospital case closed, its surgery details added after its report was sent (MC20): <c>{labCaseId, number}</c>.</summary>
    LabCaseClosed = 58,

    /// <summary>
    /// A consult was sent to another team (MC25). Consult entries (59 to 62) hold <c>{consultId, askingTeamId,
    /// askingTeam, askedTeamId, askedTeam}</c> (the teams' admin-written names); never the question or the reply.
    /// </summary>
    ConsultRequested = 59,

    /// <summary>The asked team replied to a consult.</summary>
    ConsultReplied = 60,

    /// <summary>The asking team closed a consult after its reply, ending the asked team's reading.</summary>
    ConsultClosed = 61,

    /// <summary>The asking team withdrew a consult before a reply, ending the asked team's reading.</summary>
    ConsultWithdrawn = 62
}
