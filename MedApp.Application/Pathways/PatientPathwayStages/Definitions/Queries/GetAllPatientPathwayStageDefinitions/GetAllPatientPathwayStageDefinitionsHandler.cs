using MedApp.Application.Pathways.Repositories;
using MedApp.Contracts.Pathways.PatientPathwayStages.Definitions.Responses;
using MediatR;

namespace MedApp.Application.Pathways.PatientPathwayStages.Definitions.Queries.GetAllPatientPathwayStageDefinitions;

public sealed class GetAllPatientPathwayStageDefinitionsHandler(IPatientPathwayStagesRepository repository)
    : IRequestHandler<GetAllPatientPathwayStageDefinitionsQuery, IReadOnlyList<PatientPathwayStageDefinitionResponse>>
{
    public async Task<IReadOnlyList<PatientPathwayStageDefinitionResponse>> Handle(
        GetAllPatientPathwayStageDefinitionsQuery request,
        CancellationToken cancellationToken)
    {
        var definitions = await repository.GetStageDefinitionsAsync(request.IncludeArchived, cancellationToken);
        var usage = await repository.GetStageDefinitionUsageAsync(definitions.Select(d => d.Id), cancellationToken);

        return definitions
            .Select(d => StageDefinitionMapper.ToResponse(d, usage.GetValueOrDefault(d.Id, StageDefinitionUsage.None)))
            .ToList();
    }
}
