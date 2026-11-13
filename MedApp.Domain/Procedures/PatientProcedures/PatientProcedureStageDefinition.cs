namespace MedApp.Domain.Procedures.PatientProcedures;

public sealed class PatientProcedureStageDefinition
{
    public Guid Id { get; init; }
    public string Name { get; set; } = string.Empty;
    
    public string Description { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;
}