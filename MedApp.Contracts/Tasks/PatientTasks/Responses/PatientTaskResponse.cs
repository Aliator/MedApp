namespace MedApp.Contracts.Tasks.PatientTasks.Responses;

public sealed class PatientTaskResponse
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

    public List<PatientTaskStageResponse> Stages { get; set; } = [];

    public List<PatientTaskAssignmentResponse> Assignments { get; set; } = [];
}