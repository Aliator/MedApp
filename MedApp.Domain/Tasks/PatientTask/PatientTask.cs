using MedApp.Domain.Patients;

namespace MedApp.Domain.PatientTask;

public sealed class PatientTask
{
    public Guid Id { get; init; }
    public string Title { get; set; } = string.Empty;
    public DateTime DueDateUtc { get; set; }
    public int CurrentStage { get; set; }
    public PatientTaskPriority Priority { get; set; }
    public PatientTaskStatus Status { get; set; }
    public string Notes { get; set; } = string.Empty;
    public Guid PatientId { get; set; }
    public Patient Patient { get; set; } = null!;
    public DateTime? AssignedAtUtc { get; set; }
    public List<Guid> AssignedUserIds { get; set; } = [];
    public DateTime CreatedAt { get; init; }
    public DateTime LastUpdated { get; set; }
    public List<PatientTaskStage> Stages { get; set; } = [];
}