namespace MedApp.Contracts.Procedures.Timeline.Responses;

/// <summary>
/// One thing that happened to one of a patient's procedures, from the procedure audit log. Which fields are set
/// depends on <see cref="Action"/>. Free-text values (notes, titles, reasons) are only sent to admins.
/// </summary>
public sealed class PatientTimelineEntryResponse
{
    public Guid Id { get; set; }

    /// <summary>The filter it falls under: Created, Stages, Notes, Assignments, Blocks, Status, DueDates, Details, Deleted or Files.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>The audit action, e.g. StageFinished.</summary>
    public string Action { get; set; } = string.Empty;

    public DateTime AtUtc { get; set; }

    /// <summary>Who did it.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>The procedure it's about; empty (all zeros) for a file attached to the patient themselves (AttachedTo Patient).</summary>
    public Guid ProcedureId { get; set; }

    /// <summary>The procedure's title now, or, once it's deleted, its title when the change was made.</summary>
    public string ProcedureTitle { get; set; } = string.Empty;

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

    /// <summary>A status change's reason (blocking and cancelling need one). Admins only.</summary>
    public string? Reason { get; set; }

    /// <summary>Assigned or Unassigned: who.</summary>
    public string? AssigneeUsername { get; set; }

    /// <summary>
    /// Details edited (title, notes, dueDate, priority) or stage notes saved (stageNotes): each field that changed.
    /// </summary>
    public List<PatientTimelineChangeResponse> Changes { get; set; } = [];

    /// <summary>A file attached or deleted (AttachmentAdded, AttachmentDeleted): which one.</summary>
    public Guid? AttachmentId { get; set; }

    /// <summary>A file attached or deleted: what it's attached to, Patient, Procedure or Stage.</summary>
    public string? AttachedTo { get; set; }

    /// <summary>A file attached or deleted: its name. Admins only (it's free text).</summary>
    public string? FileName { get; set; }
}
