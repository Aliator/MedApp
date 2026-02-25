using MedApp.Application.Tasks.Repositories;
using MedApp.Contracts.Tasks.PatientTaskStages.Definitions.Responses;
using MedApp.Domain.Tasks.PatientTasks;
using MediatR;

namespace MedApp.Application.Tasks.PatientTaskStages.Definitions.Commands.CreatePatientTaskStageDefinition;

public sealed class CreatePatientTaskStageDefinitionHandler(IPatientTaskStagesRepository repository)
    : IRequestHandler<CreatePatientTaskStageDefinitionCommand, PatientTaskStageDefinitionResponse>
{
    public async Task<PatientTaskStageDefinitionResponse> Handle(
        CreatePatientTaskStageDefinitionCommand request,
        CancellationToken cancellationToken)
    {
        var stageDefinition = new PatientTaskStageDefinition
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            Instructions = request.Instructions,
        };

        await repository.AddStageDefinitionAsync(stageDefinition, cancellationToken);

        return new PatientTaskStageDefinitionResponse
        {
            Id = stageDefinition.Id,
            Name = stageDefinition.Name,
            Description = stageDefinition.Description,
            Instructions = stageDefinition.Instructions,
        };
    }
}