namespace MedApp.Contracts.Procedures.Timeline.Responses;

/// <summary>
/// One thing that happened to one of a patient's procedures (from the procedure audit log) or appointments (from their
/// history). Which fields are set depends on <see cref="Action"/>. Free-text values (notes, titles, reasons) are only
/// sent to admins.
/// </summary>
public sealed class PatientTimelineEntryResponse
{
    public Guid Id { get; set; }

    /// <summary>
    /// The filter it falls under: Created, Stages, Notes, Assignments, Blocks, Status, DueDates, Details, Deleted or
    /// Appointments.
    /// </summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>The audit action, e.g. StageFinished, or for an appointment "Appointment" and what happened, e.g. AppointmentBooked.</summary>
    public string Action { get; set; } = string.Empty;

    public DateTime AtUtc { get; set; }

    /// <summary>Who did it.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>The procedure; null for an appointment not for a procedure.</summary>
    public Guid? ProcedureId { get; set; }

    /// <summary>The procedure's title now, or, once it's deleted, its title when the change was made. Null without a procedure.</summary>
    public string? ProcedureTitle { get; set; }

    /// <summary>The procedure has since been deleted (only admins see these entries).</summary>
    public bool IsProcedureDeleted { get; set; }

    /// <summary>The stage it was about, if any.</summary>
    public string? StageName { get; set; }

    /// <summary>Created: the template it was made from.</summary>
    public string? TemplateName { get; set; }

    /// <summary>Created: how many stages it was given.</summary>
    public int? StageCount { get; set; }

    /// <summary>Created: its priority.</summary>
    public string? Priority { get; set; }

    /// <summary>Created: its due date.</summary>
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
}
