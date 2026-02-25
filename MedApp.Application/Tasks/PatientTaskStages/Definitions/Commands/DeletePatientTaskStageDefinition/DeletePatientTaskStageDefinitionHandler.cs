using MedApp.Application.Tasks.Repositories;
using MediatR;

namespace MedApp.Application.Tasks.PatientTaskStages.Definitions.Commands.DeletePatientTaskStageDefinition;

public sealed class DeletePatientTaskStageDefinitionHandler(IPatientTaskStagesRepository repository)
    : IRequestHandler<DeletePatientTaskStageDefinitionCommand, bool>
{
    public async Task<bool> Handle(DeletePatientTaskStageDefinitionCommand request, CancellationToken cancellationToken)
    {
        var stageDefinition = await repository.GetStageDefinitionByIdAsync(request.StageDefinitionId, cancellationToken);

        if (stageDefinition is null)
        {
            return false;
        }

        await repository.DeleteStageDefinitionAsync(request.StageDefinitionId, cancellationToken);

        return true;
    }
}