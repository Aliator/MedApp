using MedApp.Domain.Procedures.PatientProcedures;

namespace MedApp.Application.Procedures.Repositories;

public interface IPatientProcedureStagesRepository
{
    Task AddStageDefinitionAsync(PatientProcedureStageDefinition definition, CancellationToken ct);
    Task<PatientProcedureStageDefinition?> GetStageDefinitionByIdAsync(Guid id, CancellationToken ct);
    Task<IEnumerable<PatientProcedureStageDefinition>> GetAllStageDefinitionsAsync(CancellationToken ct);
    Task UpdateStageDefinitionAsync(PatientProcedureStageDefinition definition, CancellationToken ct);
    Task DeleteStageDefinitionAsync(Guid id, CancellationToken ct);

    Task AddStageTemplateAsync(PatientProcedureStageTemplate template, CancellationToken ct);
    Task<PatientProcedureStageTemplate?> GetStageTemplateByIdAsync(Guid id, CancellationToken ct);
    Task<IEnumerable<PatientProcedureStageTemplate>> GetAllStageTemplatesAsync(CancellationToken ct);
    Task UpdateStageTemplateAsync(PatientProcedureStageTemplate template, CancellationToken ct);
    Task DeleteStageTemplateAsync(Guid id, CancellationToken ct);

    Task ReplaceStageTemplateMapsAsync(Guid templateId, IEnumerable<Guid> stageDefinitionIdsInOrder, CancellationToken ct);
}