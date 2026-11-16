namespace MedApp.Contracts.Patients.Responses;

/// <summary>
/// Everything MedApp holds about a patient, for a subject access request (Article 15) or portability (Article 20):
/// their details, procedures with stages, notes and assignments, the history of changes to their procedures, who has
/// accessed their record, and the notifications about their procedures. Staff appear by username only: no account IDs,
/// network addresses or read times, which are the staff's data, not the patient's. The printable page shows exactly
/// these fields.
/// </summary>
public sealed class PatientExportResponse
{
    public DateTime ExportedAtUtc { get; set; }

    /// <summary>The admin who made the export.</summary>
    public string ExportedBy { get; set; } = string.Empty;

    public ExportedPatientResponse Patient { get; set; } = new();

    /// <summary>The patient's procedures, oldest first.</summary>
    public List<ExportedProcedureResponse> Procedures { get; set; } = [];

    /// <summary>Every change to the patient's procedures, oldest first, including procedures since deleted.</summary>
    public List<ExportedHistoryEntryResponse> ProcedureHistory { get; set; } = [];

    /// <summary>Who viewed, changed or exported the record, oldest first (this export included).</summary>
    public List<ExportedAccessEntryResponse> AccessLog { get; set; } = [];

    /// <summary>Notifications sent to staff about the patient's procedures.</summary>
    public List<ExportedNotificationResponse> Notifications { get; set; } = [];
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

/// <summary>One of the patient's procedures, as the export gives it.</summary>
public sealed class ExportedProcedureResponse
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
    public string Notes { get; set; } = string.Empty;

    /// <summary>The usernames of the staff assigned to it.</summary>
    public List<string> AssignedTo { get; set; } = [];

    public List<ExportedStageResponse> Stages { get; set; } = [];
}

/// <summary>One stage of a procedure, as the export gives it.</summary>
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

/// <summary>One change to one of the patient's procedures, as the export gives it.</summary>
public sealed class ExportedHistoryEntryResponse
{
    public DateTime AtUtc { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public Guid ProcedureId { get; set; }
    public string ProcedureTitle { get; set; } = string.Empty;
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
    public Guid? ProcedureId { get; set; }

    /// <summary>More about it, as JSON (the page puts it in words).</summary>
    public string Details { get; set; } = "{}";
}

/// <summary>A notification about one of the patient's procedures, and who it was for.</summary>
public sealed class ExportedNotificationResponse
{
    public DateTime CreatedAtUtc { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Guid? ProcedureId { get; set; }
}
