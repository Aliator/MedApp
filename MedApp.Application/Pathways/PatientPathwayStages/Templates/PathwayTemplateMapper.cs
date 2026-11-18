using MedApp.Contracts.Pathways.PatientPathwayStages.Templates.Responses;
using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.Application.Pathways.PatientPathwayStages.Templates;

public static class PathwayTemplateMapper
{
    /// <summary>
    /// A template's response. Stage names come from <paramref name="definitions"/> when given (for a template just
    /// built, whose maps have no definitions loaded), otherwise from each map's definition.
    /// </summary>
    public static PathwayTemplateResponse ToResponse(
        PathwayTemplate template,
        int pathwayCount,
        IReadOnlyDictionary<Guid, PatientPathwayStageDefinition>? definitions = null)
    {
        var maps = template.Maps.OrderBy(m => m.StageOrder).ToList();

        return new PathwayTemplateResponse
        {
            Id = template.Id,
            Name = template.Name,
            IsArchived = template.IsArchived,
            PathwayCount = pathwayCount,
            Stages = maps
                .Select(m =>
                {
                    var definition = definitions?.GetValueOrDefault(m.StageDefinitionId) ?? m.StageDefinition;
                    return new PathwayTemplateStageResponse
                    {
                        StageOrder = m.StageOrder,
                        StageDefinitionId = m.StageDefinitionId,
                        Name = definition?.Name ?? string.Empty,
                        Description = definition?.Description ?? string.Empty,
                        SnomedCode = definition?.SnomedCode,
                        SnomedTerm = definition?.SnomedTerm,
                        IsArchived = definition?.IsArchived ?? false,
                        VisitRequirement = (definition?.VisitRequirement ?? VisitRequirement.None).ToString()
                    };
                })
                .ToList(),
            StageDefinitionIdsInOrder = maps.Select(m => m.StageDefinitionId).ToList()
        };
    }

    /// <summary>A stage list as template rows, numbered from 1 in the order given (repeats allowed).</summary>
    public static List<PathwayTemplateMap> ToMaps(Guid templateId, IEnumerable<Guid> stageDefinitionIdsInOrder) =>
        stageDefinitionIdsInOrder
            .Select((id, index) => new PathwayTemplateMap
            {
                TemplateId = templateId,
                StageDefinitionId = id,
                StageOrder = index + 1
            })
            .ToList();
}
