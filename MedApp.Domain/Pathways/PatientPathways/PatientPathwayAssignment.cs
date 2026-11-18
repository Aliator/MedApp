namespace MedApp.Domain.Pathways.PatientPathways;

public sealed class PatientPathwayAssignment
{
    public Guid PatientPathwayId { get; set; }
    public PatientPathway PatientPathway { get; set; } = null!;

    public Guid UserId { get; set; }
    public Guid AssignedByUserId { get; set; }

    public DateTime AssignedAtUtc { get; set; }
}