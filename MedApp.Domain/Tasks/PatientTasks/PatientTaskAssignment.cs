namespace MedApp.Domain.Tasks.PatientTasks;

public sealed class PatientTaskAssignment
{
    public Guid PatientTaskId { get; set; }
    public PatientTask PatientTask { get; set; } = null!;

    public Guid UserId { get; set; }
    public Guid AssignedByUserId { get; set; }

    public DateTime AssignedAtUtc { get; set; }
}