namespace MedApp.Contracts.Procedures.PatientProcedureStages.Definitions.Requests;

public sealed class CreatePatientProcedureStageDefinitionRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;

    /// <summary>Whether the stage involves a visit: None (the default), Suggested or Required.</summary>
    public string VisitRequirement { get; set; } = "None";
}