using MedApp.Application.Common.Exceptions;
using MedApp.Application.Pathways.Repositories;
using MedApp.Contracts.Pathways.PatientPathwayStages.Definitions.Responses;
using MediatR;

namespace MedApp.Application.Pathways.PatientPathwayStages.Definitions.Commands.SetPatientPathwayStageDefinitionArchived;

public sealed class SetPatientPathwayStageDefinitionArchivedHandler(IPatientPathwayStagesRepository repository)
    : IRequestHandler<SetPatientPathwayStageDefinitionArchivedCommand, PatientPathwayStageDefinitionResponse>
{
    public async Task<PatientPathwayStageDefinitionResponse> Handle(
        SetPatientPathwayStageDefinitionArchivedCommand request,
        CancellationToken cancellationToken)
    {
        var definition = await repository.GetStageDefinitionByIdAsync(request.StageDefinitionId, cancellationToken)
                         ?? throw new NotFoundException($"Stage definition '{request.StageDefinitionId}' was not found.");

        if (definition.IsArchived != request.IsArchived)
        {
            definition.IsArchived = request.IsArchived;
            await repository.UpdateStageDefinitionAsync(definition, cancellationToken);
        }

        return await StageDefinitionMapper.ToResponseAsync(definition, repository, cancellationToken);
    }
}
