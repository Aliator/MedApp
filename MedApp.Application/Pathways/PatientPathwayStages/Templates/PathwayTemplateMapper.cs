using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Pathways.PatientPathwayStages.Templates.Responses;
using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.Application.Pathways.PatientPathwayStages.Templates;

public static class PathwayTemplateMapper
{
    /// <summary>
    /// A template's response. Stage names come from <paramref name="definitions"/> when given (for a template just
    /// built, whose maps have no definitions loaded, and for its addable branches' stages, which are kept by ID),
    /// otherwise from each map's definition. Owners are named from <paramref name="teamNames"/>, else the team loaded with
    /// each map. A template without links (one written as a list before routes) shows its chain.
    /// </summary>
    public static PathwayTemplateResponse ToResponse(
        PathwayTemplate template,
        int pathwayCount,
        IReadOnlyDictionary<Guid, PatientPathwayStageDefinition>? definitions = null,
        IReadOnlyDictionary<Guid, string>? teamNames = null)
    {
        var maps = template.Maps.OrderBy(m => m.StageOrder).ToList();

        return new PathwayTemplateResponse
        {
            Id = template.Id,
            Name = template.Name,
            IsArchived = template.IsArchived,
            TeamId = template.TeamId,
            TeamName = template.Team?.Name ?? string.Empty,
            RetentionClass = template.RetentionClass.ToString(),
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
                        VisitRequirement = (definition?.VisitRequirement ?? VisitRequirement.None).ToString(),
                        IsOptional = m.IsOptional,
                        ChoiceKind = m.ChoiceKind?.ToString(),
                        Outcomes = m.ChoiceKind is null
                            ? null
                            : m.Outcomes
                                .Select(o => new PathwayTemplateOutcomeResponse { Name = o.Name, NextStageOrders = o.NextStageOrders.ToList() })
                                .ToList(),
                        StopsClock = m.StopsClock,
                        OwnerTeamId = m.OwnerTeamId,
                        OwnerTeamName = m.OwnerTeamId is { } owner
                            ? teamNames?.GetValueOrDefault(owner) ?? m.OwnerTeam?.Name
                            : null
                    };
                })
                .ToList(),
            StageDefinitionIdsInOrder = maps.Select(m => m.StageDefinitionId).ToList(),
            ClockStopStageOrder = maps.FirstOrDefault(m => m.StopsClock)?.StageOrder,
            Links = (template.Links.Count > 0 ? template.Links : PathwayGraph.ChainLinks(maps.Count))
                .Select(l => new PathwayTemplateLinkResponse { FromStageOrder = l.FromStageOrder, ToStageOrder = l.ToStageOrder })
                .ToList(),
            AddableBranches = template.AddableBranches
                .Select(b => new PathwayTemplateBranchResponse
                {
                    Id = b.Id,
                    Name = b.Name,
                    Stages = b.StageDefinitionIds
                        .Select(id => new PathwayTemplateBranchStageResponse
                        {
                            StageDefinitionId = id,
                            Name = definitions?.GetValueOrDefault(id)?.Name ?? string.Empty
                        })
                        .ToList(),
                    AttachAfterStageOrders = b.AttachAfterStageOrders.ToList(),
                    IsRepeatable = b.IsRepeatable
                })
                .ToList()
        };
    }

    /// <summary>The stage definitions a template's addable branches name, for their stages' names.</summary>
    public static IReadOnlyList<Guid> BranchDefinitionIds(IEnumerable<PathwayTemplate> templates) =>
        templates.SelectMany(t => t.AddableBranches).SelectMany(b => b.StageDefinitionIds).Distinct().ToList();
}
