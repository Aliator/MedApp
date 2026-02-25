using MedApp.Application.Common.Exceptions;
using MedApp.Application.Tasks.Repositories;
using MedApp.Contracts.Tasks.PatientTaskStages.Definitions.Responses;
using MediatR;

namespace MedApp.Application.Tasks.PatientTaskStages.Definitions.Queries.GetPatientTaskStageDefinitionById;

public sealed class GetPatientTaskStageDefinitionByIdHandler(IPatientTaskStagesRepository repository)
    : IRequestHandler<GetPatientTaskStageDefinitionByIdQuery, PatientTaskStageDefinitionResponse>
{
    public async Task<PatientTaskStageDefinitionResponse> Handle(
        GetPatientTaskStageDefinitionByIdQuery request,
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