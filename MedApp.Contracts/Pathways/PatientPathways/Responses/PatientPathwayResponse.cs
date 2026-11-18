using MedApp.Contracts.Patients.Responses;

namespace MedApp.Contracts.Pathways.PatientPathways.Responses;

/// <summary>A pathway in full, as its page shows it.</summary>
public sealed class PatientPathwayResponse
{
    public Guid Id { get; set; }
    public Guid PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;

    /// <summary>Who the pathway is for, as the patient banner shows them, or null if that isn't loaded.</summary>
    public PathwayPatientResponse? Patient { get; set; }

    public Guid? TemplateId { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;

    /// <summary>The day it's due by (UK time).</summary>
    public DateOnly DueDate { get; set; }
    public DateTime DueDateUtc { get; set; }

    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;

    /// <summary>For a Blocked pathway, the status it goes back to when unblocked.</summary>
    public string? BlockedFromStatus { get; set; }

    public bool IsOverdue { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastUpdated { get; set; }

    /// <summary>The stage being worked on: the first one not finished. Null when every stage is finished.</summary>
    public Guid? CurrentStageId { get; set; }
    public int CompletedStageCount { get; set; }
    public int StageCount { get; set; }

    /// <summary>The manual status changes allowed from here (see ChangePatientPathwayStatusRequest).</summary>
    public List<string> AllowedStatusChanges { get; set; } = [];

    /// <summary>Whether stages can be worked on in the current status.</summary>
    public bool CanWorkOnStages { get; set; }

    /// <summary>Whether the title, target date and priority can be edited (not when Cancelled or Archived).</summary>
    public bool CanEditDetails { get; set; }

    /// <summary>
    /// Whether notes can be added (and your own edited): as for the details, so also once Completed, but not when
    /// Cancelled or Archived.
    /// </summary>
    public bool CanAddNotes { get; set; }

    /// <summary>Whether an admin can assign or unassign staff (not when Completed, Cancelled or Archived).</summary>
    public bool CanChangeAssignments { get; set; }

    /// <summary>Only Cancelled and Archived pathways can be deleted (by an admin).</summary>
    public bool CanBeDeleted { get; set; }

    /// <summary>
    /// Why the pathway can't be changed now, because its patient's record is restricted or anonymised; null when it
    /// can. While it's set, every Can... flag is false, and only blocking and cancelling may be allowed (a restricted
    /// record's pathways can still be paused).
    /// </summary>
    public string? ReadOnlyReason { get; set; }

    /// <summary>The patient's restricted, disputed and anonymised flags, for the banners on the pathway's page.</summary>
    public PatientFlagsResponse? PatientFlags { get; set; }

    public List<PatientPathwayStageResponse> Stages { get; set; } = [];
    public List<PatientPathwayAssignmentResponse> Assignments { get; set; } = [];

    /// <summary>
    /// Deprecated: the pathway's first note's text (or, with none, the old single notes field), as a string, as it was
    /// before notes became a list. It's kept for one release, so a page loaded before that change doesn't fail on a list
    /// here. Read <see cref="NoteList"/>. Remove it with the old column (<c>docs/todo.md</c>).
    /// </summary>
    public string Notes { get; set; } = string.Empty;

    /// <summary>Its notes, newest first.</summary>
    public List<PatientPathwayNoteResponse> NoteList { get; set; } = [];
}
