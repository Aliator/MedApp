namespace MedApp.Contracts.Procedures.PatientProcedures.Responses;

public sealed class PatientProcedureAssignmentResponse
{
    public Guid PatientProcedureId { get; set; }
    public Guid UserId { get; set; }

    public Guid AssignedByUserId { get; set; }

    public DateTime AssignedAtUtc { get; set; }
}