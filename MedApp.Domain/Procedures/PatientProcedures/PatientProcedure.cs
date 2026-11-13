using MedApp.Domain.Patients;

namespace MedApp.Domain.Procedures.PatientProcedures;

public sealed class PatientProcedure
{
    public Guid Id { get; init; }

    public string Title { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;

    public DateTime DueDateUtc { get; set; }

    public PatientProcedurePriority Priority { get; set; }
    public PatientProcedureStatus Status { get; set; }

    public Guid PatientId { get; set; }
    public Patient Patient { get; set; } = null!;

    public DateTime CreatedAt { get; init; }
    public DateTime LastUpdated { get; set; }

    public List<PatientProcedureStage> Stages { get; set; } = [];
    public List<PatientProcedureAssignment> Assignments { get; set; } = [];
}