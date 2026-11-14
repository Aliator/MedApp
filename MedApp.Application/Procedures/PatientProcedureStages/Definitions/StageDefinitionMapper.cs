using MedApp.Application.Procedures.Repositories;
using MedApp.Contracts.Procedures.PatientProcedureStages.Definitions.Responses;
using MedApp.Domain.Procedures.PatientProcedures;

namespace MedApp.Application.Procedures.PatientProcedureStages.Definitions;

public static class StageDefinitionMapper
{
    public static PatientProcedureStageDefinitionResponse ToResponse(
        PatientProcedureStageDefinition definition,
        StageDefinitionUsage usage) => new()
    {
        Id = definition.Id,
        Name = definition.Name,
        Description = definition.Description,
        Instructions = definition.Instructions,
        IsArchived = definition.IsArchived,
        TemplateCount = usage.TemplateCount,
        CanBeDeleted = !usage.InUse
    };

    /// <summary>The response for one definition, looking up how much it's used.</summary>
    public static async Task<PatientProcedureStageDefinitionResponse> ToResponseAsync(
        PatientProcedureStageDefinition definition,
        IPatientProcedureStagesRepository repository,
        CancellationToken ct)
    {
        var usage = await repository.GetStageDefinitionUsageAsync([definition.Id], ct);
        return ToResponse(definition, usage.GetValueOrDefault(definition.Id, StageDefinitionUsage.None));
    }
}
