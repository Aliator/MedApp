namespace MedApp.Domain.Procedures.PatientProcedures;

public sealed class PatientProcedureStageTemplateMap
{
    public Guid TemplateId { get; set; }
    public PatientProcedureStageTemplate Template { get; set; } = null!;

    public Guid StageDefinitionId { get; set; }
    public PatientProcedureStageDefinition StageDefinition { get; set; } = null!;

    public int StageOrder { get; set; }
}