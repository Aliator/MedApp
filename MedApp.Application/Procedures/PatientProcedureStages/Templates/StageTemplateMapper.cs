using MedApp.Contracts.Procedures.PatientProcedureStages.Templates.Responses;
using MedApp.Domain.Procedures.PatientProcedures;

namespace MedApp.Application.Procedures.PatientProcedureStages.Templates;

public static class StageTemplateMapper
{
    /// <summary>
    /// A template's response. Stage names come from <paramref name="definitions"/> when given (for a template just
    /// built, whose maps have no definitions loaded), otherwise from each map's definition.
    /// </summary>
    public static PatientProcedureStageTemplateResponse ToResponse(
        PatientProcedureStageTemplate template,
        int procedureCount,
        IReadOnlyDictionary<Guid, PatientProcedureStageDefinition>? definitions = null)
    {
        var maps = template.Maps.OrderBy(m => m.StageOrder).ToList();

        return new PatientProcedureStageTemplateResponse
        {
            Id = template.Id,
            Name = template.Name,
            IsArchived = template.IsArchived,
            ProcedureCount = procedureCount,
            Stages = maps
                .Select(m =>
                {
                    var definition = definitions?.GetValueOrDefault(m.StageDefinitionId) ?? m.StageDefinition;
                    return new PatientProcedureTemplateStageResponse
                    {
                        StageOrder = m.StageOrder,
                        StageDefinitionId = m.StageDefinitionId,
                        Name = definition?.Name ?? string.Empty,
                        Description = definition?.Description ?? string.Empty,
                        IsArchived = definition?.IsArchived ?? false
                    };
                })
                .ToList(),
            StageDefinitionIdsInOrder = maps.Select(m => m.StageDefinitionId).ToList()
        };
    }

    /// <summary>A stage list as template rows, numbered from 1 in the order given (repeats allowed).</summary>
    public static List<PatientProcedureStageTemplateMap> ToMaps(Guid templateId, IEnumerable<Guid> stageDefinitionIdsInOrder) =>
        stageDefinitionIdsInOrder
            .Select((id, index) => new PatientProcedureStageTemplateMap
            {
                TemplateId = templateId,
                StageDefinitionId = id,
                StageOrder = index + 1
            })
            .ToList();
}
