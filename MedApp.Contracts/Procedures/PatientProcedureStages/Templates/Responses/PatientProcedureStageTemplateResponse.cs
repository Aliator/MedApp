namespace MedApp.Contracts.Procedures.PatientProcedureStages.Templates.Responses;

public sealed class PatientProcedureStageTemplateResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsArchived { get; set; }

    /// <summary>How many procedures were created from it. A template that has been used can only be archived.</summary>
    public int ProcedureCount { get; set; }

    public List<PatientProcedureTemplateStageResponse> Stages { get; set; } = [];

    /// <summary>The stage definition IDs in order (the same as <see cref="Stages"/>), for editing.</summary>
    public List<Guid> StageDefinitionIdsInOrder { get; set; } = [];
}

public sealed class PatientProcedureTemplateStageResponse
{
    public int StageOrder { get; set; }
    public Guid StageDefinitionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsArchived { get; set; }
}
