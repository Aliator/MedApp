namespace MedApp.Contracts.Tasks.PatientTasks.Requests;

public sealed class AssignPatientTaskRequest
{
    public Guid PatientTaskId { get; set; }

    public List<Guid> AssignedUserIds { get; set; } = [];

    public Guid AssignedByUserId { get; set; }
}