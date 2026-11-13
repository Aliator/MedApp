namespace MedApp.Domain.Procedures.PatientProcedures;

public sealed class PatientProcedureStageTemplate
{
    public Guid Id { get; init; }

    public string Name { get; set; } = string.Empty;

    public List<PatientProcedureStageTemplateMap> Maps { get; set; } = [];
}