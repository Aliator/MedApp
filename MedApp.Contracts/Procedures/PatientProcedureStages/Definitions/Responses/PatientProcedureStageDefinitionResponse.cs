namespace MedApp.Contracts.Procedures.PatientProcedureStages.Definitions.Responses;

public sealed class PatientProcedureStageDefinitionResponse
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;
}