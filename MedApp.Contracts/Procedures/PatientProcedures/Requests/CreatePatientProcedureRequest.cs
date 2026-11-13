namespace MedApp.Contracts.Procedures.PatientProcedures.Requests;

public sealed class CreatePatientProcedureRequest
{
    public Guid PatientId { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;

    public DateTime DueDateUtc { get; set; }

    public string Priority { get; set; } = string.Empty;

    public List<Guid> StageDefinitionIdsInOrder { get; set; } = [];
}