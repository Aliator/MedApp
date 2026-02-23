namespace MedApp.Domain.Tasks.PatientTask;

public sealed class PatientTaskStage
{
    public Guid Id { get; init; }
    public Guid TaskId { get; set; }
    public Task TaskI { get; set; } = null!;
    public int StageOrder { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime LastUpdated { get; set; }
}