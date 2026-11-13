namespace MedApp.Contracts.Procedures.PatientProcedures.Requests;

public sealed class UpdatePatientProcedureRequest
{
    public Guid PatientProcedureId { get; set; }

    public string? Title { get; set; }
    public string? Notes { get; set; }

    public DateTime? DueDateUtc { get; set; }

    public string? Priority { get; set; }
    public string? Status { get; set; }

    public List<Guid>? StageDefinitionIdsInOrder { get; set; }
}