namespace MedApp.Contracts.Tasks.PatientTasks.Requests;

public sealed class CreatePatientTaskRequest
{
    public Guid PatientId { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;

    public DateTime DueDateUtc { get; set; }

    public string Priority { get; set; } = string.Empty;

    public List<Guid> StageDefinitionIdsInOrder { get; set; } = [];

    public List<Guid> AssignedUserIds { get; set; } = [];
}