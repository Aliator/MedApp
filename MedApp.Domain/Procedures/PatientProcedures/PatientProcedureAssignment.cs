namespace MedApp.Domain.Procedures.PatientProcedures;

public sealed class PatientProcedureAssignment
{
    public Guid PatientProcedureId { get; set; }
    public PatientProcedure PatientProcedure { get; set; } = null!;

    public Guid UserId { get; set; }
    public Guid AssignedByUserId { get; set; }

    public DateTime AssignedAtUtc { get; set; }
}