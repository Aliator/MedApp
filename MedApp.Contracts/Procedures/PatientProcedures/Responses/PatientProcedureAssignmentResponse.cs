namespace MedApp.Contracts.Procedures.PatientProcedures.Responses;

public sealed class PatientProcedureAssignmentResponse
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public Guid AssignedByUserId { get; set; }
    public string AssignedByUsername { get; set; } = string.Empty;
    public DateTime AssignedAtUtc { get; set; }
}
