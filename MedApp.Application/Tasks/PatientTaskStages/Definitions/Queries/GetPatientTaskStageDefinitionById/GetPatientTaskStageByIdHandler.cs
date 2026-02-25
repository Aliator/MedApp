using MedApp.Application.Common.Exceptions;
using MedApp.Application.Tasks.Repositories;
using MedApp.Contracts.Tasks.PatientTaskStageDefinitions.Responses;
using MediatR;

namespace MedApp.Application.Tasks.PatientTaskStages.Definitions.Queries.GetPatientTaskStageDefinitionById;

public sealed class GetPatientTaskStageByIdHandler(IPatientTaskStagesRepository repository)
    : IRequestHandler<GetPatientTaskStageByIdQuery, PatientTaskStageDefinitionResponse>
{
    public async Task<PatientTaskStageDefinitionResponse> Handle(
        GetPatientTaskStageByIdQuery request,
        CancellationToken cancellationToken)
    {
        var stageDefinition = await repository.GetStageDefinitionByIdAsync(request.StageDefinitionId, cancellationToken)
                              ?? throw new NotFoundException($"Patient task stage definition '{request.StageDefinitionId}' was not found.");

        return new PatientTaskStageDefinitionResponse
        {
            Id = stageDefinition.Id,
            Name = stageDefinition.Name,
            Description = stageDefinition.Description,
            Instructions = stageDefinition.Instructions
        };
    }
}