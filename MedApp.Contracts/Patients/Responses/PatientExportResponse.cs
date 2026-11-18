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
    /// The patient's specimens, oldest taken first: where each came from, where it went and what happened to it. Their
    /// history is in <c>PathwayHistory</c>; their reports are among <c>Attachments</c>.
    /// </summary>
    public List<ExportedSpecimenResponse> Specimens { get; set; } = [];
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

    /// <summary>The pathway it was accepted into, if any.</summary>
    public Guid? PathwayId { get; set; }
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
