namespace MedApp.Contracts.Procedures.PatientProcedures.Requests;

public sealed class AssignPatientProcedureRequest
{
    public Guid PatientProcedureId { get; set; }

    public List<Guid> AssignedUserIds { get; set; } = [];

    public Guid AssignedByUserId { get; set; }
}