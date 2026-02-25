namespace MedApp.Contracts.Tasks.PatientTasks.Responses;

public sealed class PatientTaskAssignmentResponse
{
    public Guid PatientTaskId { get; set; }
    public Guid UserId { get; set; }

    public Guid AssignedByUserId { get; set; }

    public DateTime AssignedAtUtc { get; set; }
}