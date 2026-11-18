namespace MedApp.Contracts.Pathways.Timeline.Responses;

/// <summary>
/// One thing that happened to one of a patient's pathways (from the pathway audit log), appointments or referrals
/// (from their histories). Which fields are set depends on <see cref="Action"/>. Free-text values (notes, titles, reasons) are only
/// sent to admins.
/// </summary>
public sealed class PatientTimelineEntryResponse
{
    public Guid Id { get; set; }

    /// <summary>
    /// The filter it falls under: Created, Stages, Notes, Assignments, Blocks, Status, DueDates, Details, Deleted,
    /// Appointments, Files or Referrals.
    /// </summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>
    /// The audit action, e.g. StageFinished, or for an appointment "Appointment" and what happened, e.g. AppointmentBooked,
    /// or for a referral "Referral" and what happened, e.g. ReferralRegistered.
    /// </summary>
    public string Action { get; set; } = string.Empty;

    public DateTime AtUtc { get; set; }

    /// <summary>Who did it.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// The pathway; null for an appointment not for a pathway, and empty (all zeros) for a file attached to the
    /// patient themselves (AttachedTo Patient).
    /// </summary>
    public Guid? PathwayId { get; set; }

    /// <summary>The pathway's title now, or, once it's deleted, its title when the change was made. Null without a pathway.</summary>
    public string? PathwayTitle { get; set; }

    /// <summary>The pathway has since been deleted (only admins see these entries).</summary>
    public bool IsPathwayDeleted { get; set; }

    /// <summary>The stage it was about, if any.</summary>
    public string? StageName { get; set; }

    /// <summary>Created: the template it was made from.</summary>
    public string? TemplateName { get; set; }

    /// <summary>Created: how many stages it was given.</summary>
    public int? StageCount { get; set; }

    /// <summary>Created: its priority.</summary>
    public string? Priority { get; set; }

    /// <summary>Created: its target date.</summary>
    public DateOnly? DueDate { get; set; }

    /// <summary>A status change: the status before. Deleted: the status it was deleted in.</summary>
    public string? FromStatus { get; set; }

    /// <summary>A status change: the new status.</summary>
    public string? ToStatus { get; set; }

    /// <summary>A status change the workflow made by itself, e.g. In review once every stage is finished.</summary>
    public bool IsAutomatic { get; set; }

    /// <summary>
    /// A status change's reason (blocking and cancelling need one), an appointment's cancellation reason, or why a stage
    /// was finished without its visit. Admins only.
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>Assigned or Unassigned: who.</summary>
    public string? AssigneeUsername { get; set; }

    /// <summary>
    /// Details edited (title, notes, dueDate, priority), stage notes saved (stageNotes) or an appointment's notes saved
    /// (appointmentNotes): each field that changed.
    /// </summary>
    public List<PatientTimelineChangeResponse> Changes { get; set; } = [];

    /// <summary>StageFinished: it was finished without an attended visit, though its stage involves one.</summary>
    public bool FinishedWithoutVisit { get; set; }

    /// <summary>StageFinished without its visit: the stage's visit requirement, Suggested or Required.</summary>
    public string? VisitRequirement { get; set; }

    /// <summary>An appointment entry: the appointment.</summary>
    public Guid? AppointmentId { get; set; }

    /// <summary>An appointment entry: its time after the change, if booked.</summary>
    public DateTime? AppointmentStartUtc { get; set; }

    public DateTime? AppointmentEndUtc { get; set; }

    /// <summary>An appointment entry: its member of staff after the change, if booked.</summary>
    public string? AppointmentStaffUsername { get; set; }

    /// <summary>AppointmentRescheduled: its time before.</summary>
    public DateTime? PreviousStartUtc { get; set; }

    /// <summary>AppointmentRescheduled: its member of staff before.</summary>
    public string? PreviousStaffUsername { get; set; }

    /// <summary>For a Did not attend's follow-up: Rebook, ClinicianToReview or NoFurtherAppointment (its detail is the reason, for admins).</summary>
    public string? DnaFollowUp { get; set; }

    /// <summary>A file attached or deleted (AttachmentAdded, AttachmentDeleted): which one.</summary>
    public Guid? AttachmentId { get; set; }

    /// <summary>A file attached or deleted: what it's attached to, Patient, Pathway, Stage or Referral.</summary>
    public string? AttachedTo { get; set; }

    /// <summary>A file attached or deleted: its name. Admins only (it's free text).</summary>
    public string? FileName { get; set; }

    /// <summary>A referral entry, or a file attached to a referral: the referral.</summary>
    public Guid? ReferralId { get; set; }

    /// <summary>
    /// A referral entry: its specialty (the one asked for when registered; the new one when an edit moved it), an
    /// admin-written name.
    /// </summary>
    public string? SpecialtyName { get; set; }

    /// <summary>A referral entry: the referring practice. Admins only (it's free text).</summary>
    public string? ReferrerPractice { get; set; }

    /// <summary>ReferralSpecialtyChanged: the specialty it was redirected from.</summary>
    public string? PreviousSpecialtyName { get; set; }

    /// <summary>ReferralRejected: why, a fixed choice by name (NotOfferedHere, DoesNotMeetCriteria, ...).</summary>
    public string? RejectionReason { get; set; }

    /// <summary>ReferralRedirectedElsewhere: where to. Admins only (it's free text).</summary>
    public string? RedirectedTo { get; set; }
}
