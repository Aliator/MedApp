using MedApp.Application.Pathways.Repositories;
using MedApp.Contracts.Pathways.PatientPathwayStages.Definitions.Responses;
using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.Application.Pathways.PatientPathwayStages.Definitions;

public static class StageDefinitionMapper
{
    public static PatientPathwayStageDefinitionResponse ToResponse(
        PatientPathwayStageDefinition definition,
        StageDefinitionUsage usage) => new()
    {
        Id = definition.Id,
        Name = definition.Name,
        Description = definition.Description,
        Instructions = definition.Instructions,
        SnomedCode = definition.SnomedCode,
        SnomedTerm = definition.SnomedTerm,
        VisitRequirement = definition.VisitRequirement.ToString(),
        IsArchived = definition.IsArchived,
        TemplateCount = usage.TemplateCount,
        CanBeDeleted = !usage.InUse
    };

    /// <summary>The response for one definition, looking up how much it's used.</summary>
    public static async Task<PatientPathwayStageDefinitionResponse> ToResponseAsync(
        PatientPathwayStageDefinition definition,
        IPatientPathwayStagesRepository repository,
        CancellationToken ct)
    {
        var usage = await repository.GetStageDefinitionUsageAsync([definition.Id], ct);
        return ToResponse(definition, usage.GetValueOrDefault(definition.Id, StageDefinitionUsage.None));
    }
}
