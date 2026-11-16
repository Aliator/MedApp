using MedApp.Contracts.Activity.Responses;
using MedApp.Contracts.Procedures.Audit.Responses;
using MedApp.Contracts.Procedures.PatientProcedures.Responses;

namespace MedApp.Contracts.Patients.Responses;

/// <summary>
/// Everything MedApp holds about a patient, for a subject access request (Article 15) or portability (Article 20):
/// their details, procedures with stages, notes and assignments, the history of changes to their procedures, who has
/// accessed their record, and the notifications about their procedures.
/// </summary>
public sealed class PatientExportResponse
{
    public DateTime ExportedAtUtc { get; set; }

    /// <summary>The admin who made the export.</summary>
    public string ExportedBy { get; set; } = string.Empty;

    public ExportedPatientResponse Patient { get; set; } = new();

    public List<PatientProcedureResponse> Procedures { get; set; } = [];

    /// <summary>Every change to the patient's procedures, oldest first, including procedures since deleted.</summary>
    public List<PatientProcedureAuditEntryResponse> ProcedureHistory { get; set; } = [];

    /// <summary>Who viewed, changed or exported the record, oldest first (this export included).</summary>
    public List<ActivityEntryResponse> AccessLog { get; set; } = [];

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

/// <summary>A notification about one of the patient's procedures, and who it was for.</summary>
public sealed class ExportedNotificationResponse
{
    public Guid Id { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Guid? ProcedureId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ReadAtUtc { get; set; }
}
