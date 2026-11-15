namespace MedApp.Contracts.Procedures.Audit.Responses;

public sealed class PatientProcedureAuditEntryResponse
{
    public Guid Id { get; set; }
    public Guid PatientProcedureId { get; set; }
    public string ProcedureTitle { get; set; } = string.Empty;
    public Guid PatientId { get; set; }
    public Guid? StageId { get; set; }
    public string? StageName { get; set; }
    public string Action { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public DateTime AtUtc { get; set; }

    /// <summary>What changed, as JSON (from/to values, notes text, reason, ...).</summary>
    public string Details { get; set; } = "{}";
}
