namespace MedApp.Contracts.Patients.Responses;

/// <summary>
/// Everything MedApp holds about a patient, for a subject access request (Article 15) or portability (Article 20):
/// their details, pathways with stages, notes and assignments, the history of changes to their pathways, their
/// appointments and the history of changes to them, who has accessed their record, the notifications about their
/// pathways and appointments, and the files attached to them (their details only: the files themselves aren't in
/// the export yet). Staff appear by username only: no account IDs,
/// network addresses or read times, which are the staff's data, not the patient's. The printable page shows exactly
/// these fields.
/// </summary>
public sealed class PatientExportResponse
{
    public DateTime ExportedAtUtc { get; set; }

    /// <summary>The admin who made the export.</summary>
    public string ExportedBy { get; set; } = string.Empty;

    public ExportedPatientResponse Patient { get; set; } = new();

    /// <summary>The patient's pathways, oldest first.</summary>
    public List<ExportedPathwayResponse> Pathways { get; set; } = [];

    /// <summary>Every change to the patient's pathways, oldest first, including pathways since deleted.</summary>
    public List<ExportedHistoryEntryResponse> PathwayHistory { get; set; } = [];

    /// <summary>
    /// Every version of every note on the patient's pathways (as added, then after each edit), oldest first, including
    /// those of pathways since deleted.
    /// </summary>
    public List<ExportedNoteVersionResponse> NoteHistory { get; set; } = [];

    /// <summary>
    /// Requests from staff to delete notes on the patient's pathways, oldest first, with their reasons and outcomes,
    /// including those about pathways since deleted.
    /// </summary>
    public List<ExportedNoteDeletionRequestResponse> NoteDeletionRequests { get; set; } = [];

    /// <summary>The patient's appointments, in any status, oldest first.</summary>
    public List<ExportedAppointmentResponse> Appointments { get; set; } = [];

    /// <summary>Every change to the patient's appointments, oldest first.</summary>
    public List<ExportedAppointmentHistoryEntryResponse> AppointmentHistory { get; set; } = [];

    /// <summary>
    /// Every text and email to the patient about their appointments, oldest first: where it went, what it said and what
    /// became of it, and those that couldn't go, with why.
    /// </summary>
    public List<ExportedPatientMessageResponse> Messages { get; set; } = [];

    /// <summary>Who viewed, changed or exported the record, oldest first (this export included).</summary>
    public List<ExportedAccessEntryResponse> AccessLog { get; set; } = [];

    /// <summary>Notifications sent to staff about the patient's pathways and appointments.</summary>
    public List<ExportedNotificationResponse> Notifications { get; set; } = [];

    /// <summary>
    /// The files attached to the patient, their pathways and stages, oldest first, deleted ones included: their
    /// details, not their contents.
    /// </summary>
    public List<ExportedAttachmentResponse> Attachments { get; set; } = [];

    /// <summary>The patient's referrals, oldest first, with what the referrer sent and what was decided.</summary>
    public List<ExportedReferralResponse> Referrals { get; set; } = [];

    /// <summary>Every change to the patient's referrals, oldest first (structured values only).</summary>
    public List<ExportedReferralHistoryEntryResponse> ReferralHistory { get; set; } = [];

    /// <summary>
    /// The patient's letters, oldest first: what each said, who wrote and approved it, and whom it went to, when and how.
    /// Their history is in <c>PathwayHistory</c>; their approved PDFs are among <c>Attachments</c>.
    /// </summary>
    public List<ExportedLetterResponse> Letters { get; set; } = [];

    /// <summary>
    /// The patient's specimens, oldest taken first: where each came from, where it went and what happened to it. Their
    /// history is in <c>PathwayHistory</c>; their reports are among <c>Attachments</c>.
    /// </summary>
    public List<ExportedSpecimenResponse> Specimens { get; set; } = [];

    /// <summary>
    /// The patient's consent records, oldest recorded first: the stage, the form, when and by whom it was signed, and
    /// any withdrawal or correction. Their history is in <c>PathwayHistory</c>; their scans are among <c>Attachments</c>.
    /// </summary>
    public List<ExportedConsentRecordResponse> ConsentRecords { get; set; } = [];
}

/// <summary>A consent record in a patient's export: everything it holds, with staff by username only.</summary>
public sealed class ExportedConsentRecordResponse
{
    public Guid Id { get; set; }
    public Guid PathwayId { get; set; }
    public Guid StageId { get; set; }
    public string FormName { get; set; } = string.Empty;
    public string FormVersion { get; set; } = string.Empty;
    public DateOnly SignedOn { get; set; }
    public string SignedBy { get; set; } = string.Empty;
    public string? RecordedBy { get; set; }
    public DateTime RecordedAtUtc { get; set; }
    public Guid? ScanAttachmentId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateOnly? WithdrawnOn { get; set; }
    public string? StatusDetail { get; set; }
    public DateTime? StatusChangedAtUtc { get; set; }
    public string? StatusChangedBy { get; set; }
}

/// <summary>A letter in a patient's export: everything it holds, with staff by username only.</summary>
public sealed class ExportedLetterResponse
{
    public Guid Id { get; set; }

    public Guid? PathwayId { get; set; }

    public Guid? ReferralId { get; set; }

    public string Kind { get; set; } = string.Empty;

    public string TemplateName { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string Letterhead { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public string Closing { get; set; } = string.Empty;

    public bool LargePrint { get; set; }

    public string Author { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime? SubmittedAtUtc { get; set; }

    /// <summary>Whom it awaits approval from, or who approved it.</summary>
    public string? Approver { get; set; }

    public DateTime? ApprovedAtUtc { get; set; }

    public string? ReturnComment { get; set; }

    public DateTime? WithdrawnAtUtc { get; set; }

    public string? WithdrawnBy { get; set; }

    public string? WithdrawnReason { get; set; }

    public Guid? PdfAttachmentId { get; set; }

    public Guid? ReplacesLetterId { get; set; }

    public List<ExportedLetterRecipientResponse> Recipients { get; set; } = [];
}

/// <summary>One recipient of a letter in a patient's export, and how it reached them.</summary>
public sealed class ExportedLetterRecipientResponse
{
    public string Kind { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? AddressLines { get; set; }

    public string? Postcode { get; set; }

    public string? Email { get; set; }

    public string DispatchStatus { get; set; } = string.Empty;

    public string? Method { get; set; }

    public DateOnly? SentOn { get; set; }

    public string? SentBy { get; set; }
}

/// <summary>A specimen in a patient's export: everything it holds, with staff by username only.</summary>
public sealed class ExportedSpecimenResponse
{
    public Guid Id { get; set; }

    public string Number { get; set; } = string.Empty;

    public Guid PathwayId { get; set; }

    public Guid StageId { get; set; }

    public string SpecimenTypeName { get; set; } = string.Empty;

    public string LaboratoryName { get; set; } = string.Empty;

    public string? Site { get; set; }

    public string? RequestNotes { get; set; }

    public string RequestedBy { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTime TakenAtUtc { get; set; }

    public string TakenBy { get; set; } = string.Empty;

    public DateTime? SentAtUtc { get; set; }

    public string? CourierReference { get; set; }

    public DateTime? ReceivedAtUtc { get; set; }

    public string? LabReference { get; set; }

    public DateTime? ReportedAtUtc { get; set; }

    public Guid? ReportAttachmentId { get; set; }

    public string? RejectionReason { get; set; }

    public string? ClosedDetail { get; set; }

    public DateTime? ClosedAtUtc { get; set; }

    public string? ClosedBy { get; set; }
}

/// <summary>A patient's record in full, as the export gives it.</summary>
public sealed class ExportedPatientResponse
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateOnly DateOfBirth { get; set; }
    public string Email { get; set; } = string.Empty;

    /// <summary>The NHS number, as its ten digits, or null when it isn't known.</summary>
    public string? NhsNumber { get; set; }

    /// <summary>The postal address's lines (none when it isn't known).</summary>
    public List<string> AddressLines { get; set; } = [];

    public string? Postcode { get; set; }

    /// <summary>A UK mobile in international form (<c>+447…</c>), or null.</summary>
    public string? MobilePhone { get; set; }

    /// <summary>How they want appointment messages: None, Text, Email or TextAndEmail.</summary>
    public string ReminderChannel { get; set; } = "None";

    /// <summary>The GP practice they're registered with, by name, or null.</summary>
    public string? PracticeName { get; set; }

    /// <summary>That practice's ODS code, or null.</summary>
    public string? PracticeOdsCode { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime LastUpdated { get; set; }
    public DateTime? RestrictedAtUtc { get; set; }
    public string? RestrictionReason { get; set; }
    public DateTime? DisputedAtUtc { get; set; }
    public string? DisputeReason { get; set; }
    public DateTime? AnonymisedAtUtc { get; set; }
    public DateOnly? RetentionKeptUntil { get; set; }
    public string? RetentionKeptReason { get; set; }
}

/// <summary>One of the patient's pathways, as the export gives it.</summary>
public sealed class ExportedPathwayResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;

    /// <summary>The day it was due by (UK time).</summary>
    public DateOnly DueDate { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime LastUpdated { get; set; }

    /// <summary>Its notes as they are now, oldest first (<see cref="PatientExportResponse.NoteHistory"/> has every version).</summary>
    public List<ExportedPathwayNoteResponse> Notes { get; set; } = [];

    /// <summary>The usernames of the staff assigned to it.</summary>
    public List<string> AssignedTo { get; set; } = [];

    public List<ExportedStageResponse> Stages { get; set; } = [];
}

/// <summary>One note on a pathway as it is now, as the export gives it: deleted notes too, saying when, by whom and why.</summary>
public sealed class ExportedPathwayNoteResponse
{
    public Guid Id { get; set; }

    /// <summary>Who wrote it, by username ("Deleted user" once gone, "Unknown" when not recorded).</summary>
    public string Author { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? LastEditedAtUtc { get; set; }

    /// <summary>Its latest version: 1 if it was never edited.</summary>
    public int Version { get; set; }

    /// <summary>When an admin deleted it (it's hidden from the pathway page, but kept); null if it isn't deleted.</summary>
    public DateTime? DeletedAtUtc { get; set; }

    /// <summary>The admin who deleted it, by username ("Deleted user" once gone); null if it isn't deleted.</summary>
    public string? DeletedBy { get; set; }

    /// <summary>Why it was deleted (WrongPatient, WrittenInError, Duplicate or Other); null if it isn't deleted.</summary>
    public string? DeletionReason { get; set; }

    /// <summary>More about why it was deleted, if the admin gave any.</summary>
    public string? DeletionDetail { get; set; }

    /// <summary>
    /// Deleted as written on the wrong patient: its text is most likely another person's data, so whoever checks the
    /// export before it's released should consider holding it back (the third-party and serious harm tests).
    /// </summary>
    public bool IsAboutAnotherPatient { get; set; }
}

/// <summary>A member of staff's request to delete a note on one of the patient's pathways, as the export gives it.</summary>
public sealed class ExportedNoteDeletionRequestResponse
{
    public Guid Id { get; set; }
    public Guid NoteId { get; set; }
    public Guid PathwayId { get; set; }

    /// <summary>Who asked, by the username recorded at the time.</summary>
    public string RequestedBy { get; set; } = string.Empty;

    public DateTime RequestedAtUtc { get; set; }

    /// <summary>Why they asked: WrongPatient, WrittenInError, Duplicate or Other.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>More about why, if they gave any.</summary>
    public string? Detail { get; set; }

    /// <summary>Pending, Approved (the note was deleted), Declined (it was kept) or Closed (without a decision).</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>The admin who decided, by the username recorded at the time; null while it's pending.</summary>
    public string? DecidedBy { get; set; }

    public DateTime? DecidedAtUtc { get; set; }
}

/// <summary>One version of one note on a pathway, as the export gives it.</summary>
public sealed class ExportedNoteVersionResponse
{
    public Guid NoteId { get; set; }
    public Guid PathwayId { get; set; }

    /// <summary>1 for the note as added, one more for each edit.</summary>
    public int Version { get; set; }

    public string Text { get; set; } = string.Empty;

    /// <summary>Who wrote this version, by the username recorded at the time ("Unknown" when not recorded).</summary>
    public string Username { get; set; } = string.Empty;

    public DateTime AtUtc { get; set; }
}

/// <summary>One stage of a pathway, as the export gives it.</summary>
public sealed class ExportedStageResponse
{
    public int Order { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public DateTime? StartedAtUtc { get; set; }
    public string? StartedBy { get; set; }
    public DateTime? FinishedAtUtc { get; set; }
    public string? FinishedBy { get; set; }

    /// <summary>When it was skipped, by whom, why (NotNeeded, PatientDeclined, Other or BranchNotChosen) and the detail given.</summary>
    public DateTime? SkippedAtUtc { get; set; }
    public string? SkippedBy { get; set; }
    public string? SkipReason { get; set; }
    public string? SkipDetail { get; set; }

    /// <summary>The branch it belongs to, if any (a name the template's author wrote).</summary>
    public string? Branch { get; set; }
}

/// <summary>One change to one of the patient's pathways, as the export gives it.</summary>
public sealed class ExportedHistoryEntryResponse
{
    public DateTime AtUtc { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public Guid PathwayId { get; set; }
    public string PathwayTitle { get; set; } = string.Empty;
    public string? StageName { get; set; }

    /// <summary>What changed, as JSON (the page puts it in words).</summary>
    public string Details { get; set; } = "{}";
}

/// <summary>One access to the patient's record, as the export gives it: who (by username), what and when.</summary>
public sealed class ExportedAccessEntryResponse
{
    public DateTime AtUtc { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public Guid? PathwayId { get; set; }

    /// <summary>More about it, as JSON (the page puts it in words).</summary>
    public string Details { get; set; } = "{}";
}

/// <summary>A file attached to the patient's record, as the export gives it: its details, not its contents.</summary>
public sealed class ExportedAttachmentResponse
{
    public Guid Id { get; set; }

    /// <summary>What it's attached to: Patient, Pathway or Stage.</summary>
    public string AttachedTo { get; set; } = string.Empty;

    public Guid? PathwayId { get; set; }

    /// <summary>The stage's name, for a stage's file whose pathway still exists.</summary>
    public string? StageName { get; set; }

    /// <summary>The referral it's attached to (its letter), if any.</summary>
    public Guid? ReferralId { get; set; }

    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;

    /// <summary>The type in words, e.g. PDF.</summary>
    public string TypeName { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    /// <summary>The SHA-256 of its bytes (64 hex digits).</summary>
    public string Sha256 { get; set; } = string.Empty;

    /// <summary>NotScanned, Clean or Infected.</summary>
    public string ScanStatus { get; set; } = string.Empty;

    public string UploadedBy { get; set; } = string.Empty;
    public DateTime UploadedAtUtc { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }
}

/// <summary>A notification about one of the patient's pathways or appointments, and who it was for.</summary>
public sealed class ExportedNotificationResponse
{
    public DateTime CreatedAtUtc { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Guid? PathwayId { get; set; }
    public Guid? AppointmentId { get; set; }
}

/// <summary>One of the patient's appointments, as the export gives it. Staff by username.</summary>
public sealed class ExportedAppointmentResponse
{
    public Guid Id { get; set; }

    /// <summary>The pathway it's for, if any (none once that pathway is deleted).</summary>
    public Guid? PathwayId { get; set; }

    /// <summary>The stage it's for, by name, if any.</summary>
    public string? StageName { get; set; }

    /// <summary>Requested, Booked, Attended, DidNotAttend, Cancelled or EnteredInError.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Who the patient sees, once booked.</summary>
    public string? StaffUsername { get; set; }

    public DateTime? StartUtc { get; set; }
    public DateTime? EndUtc { get; set; }
    public string Notes { get; set; } = string.Empty;
    public string? CancelReason { get; set; }

    /// <summary>Whether the app asked for it (a stage that involves a visit became current), rather than someone.</summary>
    public bool IsAutomatic { get; set; }

    /// <summary>
    /// Recorded after the event (a walk-in, or one booked outside MedApp) rather than booked ahead: then BookedAtUtc
    /// and BookedBy are when and by whom it was recorded.
    /// </summary>
    public bool IsRecordedAfterEvent { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? BookedAtUtc { get; set; }
    public string? BookedBy { get; set; }
    public DateTime? OutcomeAtUtc { get; set; }
    public string? OutcomeBy { get; set; }

    /// <summary>After a Did not attend, the follow-up recorded: Rebook, ClinicianToReview or NoFurtherAppointment.</summary>
    public string? DnaFollowUp { get; set; }

    public string? DnaFollowUpDetail { get; set; }
    public DateTime? DnaFollowUpAtUtc { get; set; }
    public string? DnaFollowUpBy { get; set; }
}

/// <summary>A text or email to the patient, as the export gives it. Who asked for it by username.</summary>
public sealed class ExportedPatientMessageResponse
{
    public Guid AppointmentId { get; set; }

    /// <summary>AppointmentReminder, AppointmentMoved, AppointmentCancelled or MissedAppointment.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Text or Email.</summary>
    public string Channel { get; set; } = string.Empty;

    /// <summary>The mobile or email address it went to; empty when it couldn't go.</summary>
    public string Destination { get; set; } = string.Empty;

    /// <summary>The text as sent; empty when it couldn't go.</summary>
    public string Body { get; set; } = string.Empty;

    public DateTime AppointmentStartUtc { get; set; }

    /// <summary>Pending, Sent, Delivered, Failed, Cancelled or NotSent.</summary>
    public string Status { get; set; } = string.Empty;

    public string? StatusReason { get; set; }

    /// <summary>Who asked for it, or null for a reminder the app sent.</summary>
    public string? RequestedBy { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public DateTime? StatusAtUtc { get; set; }
}

/// <summary>One change to one of the patient's appointments, as the export gives it.</summary>
public sealed class ExportedAppointmentHistoryEntryResponse
{
    public DateTime AtUtc { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public Guid AppointmentId { get; set; }
    public Guid? PathwayId { get; set; }

    /// <summary>The pathway's title at the time, or empty for an appointment not for a pathway.</summary>
    public string PathwayTitle { get; set; } = string.Empty;

    public string? StageName { get; set; }

    /// <summary>The member of staff after the change, by the username recorded at the time.</summary>
    public string? StaffUsername { get; set; }

    public DateTime? StartUtc { get; set; }
    public DateTime? EndUtc { get; set; }

    /// <summary>What changed, as JSON (the page puts it in words).</summary>
    public string Details { get; set; } = "{}";
}

/// <summary>A referral in a patient's export: everything it holds, with staff by username only.</summary>
public sealed class ExportedReferralResponse
{
    public Guid Id { get; set; }
    public string SpecialtyName { get; set; } = string.Empty;
    public DateOnly ReceivedOn { get; set; }
    public string ReferrerPractice { get; set; } = string.Empty;
    public string? ReferrerClinician { get; set; }
    public string? ReferrerOdsCode { get; set; }
    public string RequestedPriority { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime RegisteredAtUtc { get; set; }
    public string RegisteredBy { get; set; } = string.Empty;
    public DateTime? DecidedAtUtc { get; set; }
    public string? DecidedBy { get; set; }
    public string? RejectionReason { get; set; }
    public string? DecisionDetail { get; set; }
    public string? RedirectedTo { get; set; }

    /// <summary>Why it was redirected here from another specialty (the latest such redirection).</summary>
    public string? RedirectDetail { get; set; }

    /// <summary>The pathway it was accepted into, if any.</summary>
    public Guid? PathwayId { get; set; }

    /// <summary>Its waiting-time clock: the target in days, the day the wait counts from, the target date, the days paused, and when and why it stopped.</summary>
    public int TargetDays { get; set; }
    public DateOnly WaitStartOn { get; set; }
    public DateOnly TargetOn { get; set; }
    public int PausedDays { get; set; }
    public DateOnly? StoppedOn { get; set; }
    public string? StopReason { get; set; }

    /// <summary>The clock's pauses, with who started and ended each (by username only).</summary>
    public List<ExportedClockPauseResponse> Pauses { get; set; } = [];
}

/// <summary>A pause of a referral's waiting-time clock, in the export.</summary>
public sealed class ExportedClockPauseResponse
{
    public DateOnly PausedOn { get; set; }
    public string PausedBy { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string? Detail { get; set; }
    public DateOnly ReviewOn { get; set; }
    public DateOnly? ResumedOn { get; set; }
    public string? ResumedBy { get; set; }
}

/// <summary>One change to a referral in a patient's export, by the username recorded at the time.</summary>
public sealed class ExportedReferralHistoryEntryResponse
{
    public DateTime AtUtc { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public Guid ReferralId { get; set; }
    public Guid? PathwayId { get; set; }

    /// <summary>What changed, as stored (JSON, structured values only), without staff account IDs.</summary>
    public string Details { get; set; } = "{}";
}
