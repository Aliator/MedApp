using MedApp.Domain.Procedures.PatientProcedures;

namespace MedApp.Application.Procedures.Repositories;

/// <summary>How much a stage definition is used.</summary>
/// <param name="TemplateCount">Templates that list it.</param>
/// <param name="ProcedureCount">Procedures with a stage made from it.</param>
public sealed record StageDefinitionUsage(int TemplateCount, int ProcedureCount)
{
    public static readonly StageDefinitionUsage None = new(0, 0);

    public bool InUse => TemplateCount > 0 || ProcedureCount > 0;
}

/// <summary>Stage definitions and procedure templates, which admins manage.</summary>
public interface IPatientProcedureStagesRepository
{
    Task AddStageDefinitionAsync(PatientProcedureStageDefinition definition, CancellationToken ct);
    Task<PatientProcedureStageDefinition?> GetStageDefinitionByIdAsync(Guid id, CancellationToken ct);

    /// <summary>Stage definitions sorted by name; archived ones only if asked.</summary>
    Task<IReadOnlyList<PatientProcedureStageDefinition>> GetStageDefinitionsAsync(bool includeArchived, CancellationToken ct);

    /// <summary>The stage definitions with these IDs (each once), archived or not.</summary>
    Task<IReadOnlyList<PatientProcedureStageDefinition>> GetStageDefinitionsByIdAsync(IEnumerable<Guid> ids, CancellationToken ct);

    /// <summary>Whether another stage definition has this name, ignoring case.</summary>
    Task<bool> StageDefinitionNameExistsAsync(string name, Guid? exceptId, CancellationToken ct);

    /// <summary>How much each of these definitions is used; a definition that's unused has no entry.</summary>
    Task<IReadOnlyDictionary<Guid, StageDefinitionUsage>> GetStageDefinitionUsageAsync(IEnumerable<Guid> ids, CancellationToken ct);

    Task UpdateStageDefinitionAsync(PatientProcedureStageDefinition definition, CancellationToken ct);
    Task DeleteStageDefinitionAsync(Guid id, CancellationToken ct);

    /// <summary>Adds a template with its stage list (Maps).</summary>
    Task AddStageTemplateAsync(PatientProcedureStageTemplate template, CancellationToken ct);

    /// <summary>A template with its stage list in order, each with its definition, read-only.</summary>
    Task<PatientProcedureStageTemplate?> GetStageTemplateByIdAsync(Guid id, CancellationToken ct);

    /// <summary>Templates sorted by name, with their stage lists; archived ones only if asked.</summary>
    Task<IReadOnlyList<PatientProcedureStageTemplate>> GetStageTemplatesAsync(bool includeArchived, CancellationToken ct);

    /// <summary>Whether another template has this name, ignoring case.</summary>
    Task<bool> StageTemplateNameExistsAsync(string name, Guid? exceptId, CancellationToken ct);

    /// <summary>How many procedures were created from each of these templates; an unused template has no entry.</summary>
    Task<IReadOnlyDictionary<Guid, int>> GetTemplateProcedureCountsAsync(IEnumerable<Guid> ids, CancellationToken ct);

    /// <summary>Saves a template's name and archived flag, and replaces its stage list when one is given.</summary>
    Task UpdateStageTemplateAsync(
        PatientProcedureStageTemplate template,
        IReadOnlyList<Guid>? stageDefinitionIdsInOrder,
        CancellationToken ct);

    Task DeleteStageTemplateAsync(Guid id, CancellationToken ct);
}
