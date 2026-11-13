namespace MedApp.Contracts.Procedures.PatientProcedureStages.Definitions.Requests;

public sealed class UpdatePatientProcedureStageDefinitionRequest
{
    public Guid StageDefinitionId { get; set; }

    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Instructions { get; set; }
}