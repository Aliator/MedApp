using MedApp.Application.Common.Exceptions;
using MedApp.Application.Pathways.Repositories;
using MediatR;

namespace MedApp.Application.Pathways.PatientPathwayStages.Definitions.Commands.DeletePatientPathwayStageDefinition;

public sealed class DeletePatientPathwayStageDefinitionHandler(IPatientPathwayStagesRepository repository)
    : IRequestHandler<DeletePatientPathwayStageDefinitionCommand>
{
    public async Task Handle(DeletePatientPathwayStageDefinitionCommand request, CancellationToken cancellationToken)
    {
        var definition = await repository.GetStageDefinitionByIdAsync(request.StageDefinitionId, cancellationToken)
                         ?? throw new NotFoundException($"Stage definition '{request.StageDefinitionId}' was not found.");

        var usage = (await repository.GetStageDefinitionUsageAsync([definition.Id], cancellationToken))
            .GetValueOrDefault(definition.Id, StageDefinitionUsage.None);

        if (usage.InUse)
            throw new ConflictException(
                $"The '{definition.Name}' stage is used by {usage.TemplateCount} template(s) and {usage.PathwayCount} pathway(s), " +
                "so it can't be deleted. Archive it instead.");

        await repository.DeleteStageDefinitionAsync(definition.Id, cancellationToken);
    }
}
