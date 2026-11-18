using MedApp.Application.Common.Exceptions;
using MedApp.Application.Pathways.Repositories;
using MedApp.Contracts.Pathways.PatientPathwayStages.Definitions.Responses;
using MediatR;

namespace MedApp.Application.Pathways.PatientPathwayStages.Definitions.Queries.GetPatientPathwayStageDefinitionById;

public sealed class GetPatientPathwayStageDefinitionByIdHandler(IPatientPathwayStagesRepository repository)
    : IRequestHandler<GetPatientPathwayStageDefinitionByIdQuery, PatientPathwayStageDefinitionResponse>
{
    public async Task<PatientPathwayStageDefinitionResponse> Handle(
        GetPatientPathwayStageDefinitionByIdQuery request,
        CancellationToken cancellationToken)
    {
        var definition = await repository.GetStageDefinitionByIdAsync(request.StageDefinitionId, cancellationToken)
                         ?? throw new NotFoundException($"Stage definition '{request.StageDefinitionId}' was not found.");

        return await StageDefinitionMapper.ToResponseAsync(definition, repository, cancellationToken);
    }
}
