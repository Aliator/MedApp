using MedApp.Application.Tasks.Repositories;
using MedApp.Contracts.Tasks.PatientTaskStageDefinitions.Responses;
using MedApp.Domain.Tasks.PatientTasks;
using MediatR;

namespace MedApp.Application.Tasks.PatientTaskStages.Definitions.Commands.CreatePatientTaskStageDefinition;

public sealed class CreatePatientTaskStageHandler(IPatientTaskStagesRepository repository)
    : IRequestHandler<CreatePatientTaskStageCommand, PatientTaskStageDefinitionResponse>
{
    public async Task<PatientTaskStageDefinitionResponse> Handle(
        CreatePatientTaskStageCommand request,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
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