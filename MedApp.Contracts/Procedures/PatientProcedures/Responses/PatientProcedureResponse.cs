namespace MedApp.Contracts.Procedures.PatientProcedures.Responses;

/// <summary>A procedure in full, as its page shows it.</summary>
public sealed class PatientProcedureResponse
{
    public Guid Id { get; set; }
    public Guid PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public Guid? TemplateId { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;

    /// <summary>The day it's due by (UK time).</summary>
    public DateOnly DueDate { get; set; }
    public DateTime DueDateUtc { get; set; }

    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;

    /// <summary>For a Blocked procedure, the status it goes back to when unblocked.</summary>
    public string? BlockedFromStatus { get; set; }

    public bool IsOverdue { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastUpdated { get; set; }

    /// <summary>The stage being worked on: the first one not finished. Null when every stage is finished.</summary>
    public Guid? CurrentStageId { get; set; }
    public int CompletedStageCount { get; set; }
    public int StageCount { get; set; }

    /// <summary>The manual status changes allowed from here (see ChangePatientProcedureStatusRequest).</summary>
    public List<string> AllowedStatusChanges { get; set; } = [];

    /// <summary>Whether stages can be worked on in the current status.</summary>
    public bool CanWorkOnStages { get; set; }

    /// <summary>Whether the title, notes, due date and priority can be edited (not when Cancelled or Archived).</summary>
    public bool CanEditDetails { get; set; }

    /// <summary>Whether an admin can assign or unassign staff (not when Completed, Cancelled or Archived).</summary>
    public bool CanChangeAssignments { get; set; }

    /// <summary>Only Cancelled and Archived procedures can be deleted (by an admin).</summary>
    public bool CanBeDeleted { get; set; }

    public List<PatientProcedureStageResponse> Stages { get; set; } = [];
    public List<PatientProcedureAssignmentResponse> Assignments { get; set; } = [];
}
