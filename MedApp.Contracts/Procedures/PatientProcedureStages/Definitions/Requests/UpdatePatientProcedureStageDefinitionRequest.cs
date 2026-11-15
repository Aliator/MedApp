namespace MedApp.Contracts.Procedures.PatientProcedureStages.Definitions.Requests;

/// <summary>Edits a stage definition. Only the fields that are set change. Existing procedures keep their copy.</summary>
public sealed class UpdatePatientProcedureStageDefinitionRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Instructions { get; set; }
}
