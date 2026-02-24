using MedApp.Domain.Tasks.PatientTasks;

namespace MedApp.Application.Tasks.Repositories;

public interface IPatientTaskStagesRepository
{
    Task AddStageDefinitionAsync(PatientTaskStageDefinition definition, CancellationToken ct);
    Task<PatientTaskStageDefinition?> GetStageDefinitionByIdAsync(Guid id, CancellationToken ct);
    Task<IEnumerable<PatientTaskStageDefinition>> GetAllStageDefinitionsAsync(CancellationToken ct);
    Task UpdateStageDefinitionAsync(PatientTaskStageDefinition definition, CancellationToken ct);
    Task DeleteStageDefinitionAsync(Guid id, CancellationToken ct);

    Task AddStageTemplateAsync(PatientTaskStageTemplate template, CancellationToken ct);
    Task<PatientTaskStageTemplate?> GetStageTemplateByIdAsync(Guid id, CancellationToken ct);
    Task<IEnumerable<PatientTaskStageTemplate>> GetAllStageTemplatesAsync(CancellationToken ct);
    Task UpdateStageTemplateAsync(PatientTaskStageTemplate template, CancellationToken ct);
    Task DeleteStageTemplateAsync(Guid id, CancellationToken ct);

    Task ReplaceStageTemplateMapsAsync(Guid templateId, IEnumerable<Guid> stageDefinitionIdsInOrder, CancellationToken ct);
}