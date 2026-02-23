namespace MedApp.Contracts.Tasks.PatientTasks.Requests;

public sealed class UpdatePatientTaskRequest
{
    public Guid PatientTaskId { get; set; }

    public string? Title { get; set; }
    public string? Notes { get; set; }

    public DateTime? DueDateUtc { get; set; }

    public string? Priority { get; set; }
    public string? Status { get; set; }

    public List<Guid>? StageDefinitionIdsInOrder { get; set; }

    public List<Guid>? AssignedUserIds { get; set; }
}