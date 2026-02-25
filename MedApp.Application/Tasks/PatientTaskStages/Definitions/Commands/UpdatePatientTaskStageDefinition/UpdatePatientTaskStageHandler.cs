using MedApp.Application.Common.Exceptions;
using MedApp.Application.Tasks.Repositories;
using MedApp.Contracts.Tasks.PatientTaskStageDefinitions.Responses;
using MediatR;

namespace MedApp.Application.Tasks.PatientTaskStages.Definitions.Commands.UpdatePatientTaskStageDefinition;

public sealed class UpdatePatientTaskStageHandler(IPatientTaskStagesRepository repository)
    : IRequestHandler<UpdatePatientTaskStageCommand, PatientTaskStageDefinitionResponse>
{
    public async Task<PatientTaskStageDefinitionResponse> Handle(
        UpdatePatientTaskStageCommand request,
        CancellationToken cancellationToken)
    {
        var stageDefinition = await repository.GetStageDefinitionByIdAsync(request.StageDefinitionId, cancellationToken)
                              ?? throw new NotFoundException($"Patient task stage definition '{request.StageDefinitionId}' was not found.");

        if (request.Name is not null) stageDefinition.Name = request.Name;
        if (request.Description is not null) stageDefinition.Description = request.Description;
        if (request.Instructions is not null) stageDefinition.Instructions = request.Instructions;

        await repository.UpdateStageDefinitionAsync(stageDefinition, cancellationToken);

        return new PatientTaskStageDefinitionResponse
        {
            Id = stageDefinition.Id,
            Name = stageDefinition.Name,
            Description = stageDefinition.Description,
            Instructions = stageDefinition.Instructions
        };
    }
}