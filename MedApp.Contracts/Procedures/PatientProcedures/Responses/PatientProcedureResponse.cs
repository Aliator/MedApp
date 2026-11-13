namespace MedApp.Contracts.Procedures.PatientProcedures.Responses;

public sealed class PatientProcedureResponse
{
    public Guid Id { get; set; }

    public Guid PatientId { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;

    public DateTime DueDateUtc { get; set; }

    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
    public DateTime LastUpdated { get; set; }

    public List<PatientProcedureStageResponse> Stages { get; set; } = [];

    public List<PatientProcedureAssignmentResponse> Assignments { get; set; } = [];
}