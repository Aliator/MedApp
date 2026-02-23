using MedApp.Domain.Patients;

namespace MedApp.Domain.Tasks.PatientTask;

public sealed class PatientTask
{
    public Guid Id { get; init; }

    public string Title { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;

    public DateTime DueDateUtc { get; set; }

    public PatientTaskPriority Priority { get; set; }
    public PatientTaskStatus Status { get; set; }

    public Guid PatientId { get; set; }
    public Patient Patient { get; set; } = null!;

    public DateTime CreatedAt { get; init; }
    public DateTime LastUpdated { get; set; }

    public List<PatientTaskStage> Stages { get; set; } = [];
    public List<PatientTaskAssignment> Assignments { get; set; } = [];
}