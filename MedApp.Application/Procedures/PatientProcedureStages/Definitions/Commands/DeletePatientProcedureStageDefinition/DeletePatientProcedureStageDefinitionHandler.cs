using MedApp.Application.Common.Exceptions;
using MedApp.Application.Procedures.Repositories;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedureStages.Definitions.Commands.DeletePatientProcedureStageDefinition;

public sealed class DeletePatientProcedureStageDefinitionHandler(IPatientProcedureStagesRepository repository)
    : IRequestHandler<DeletePatientProcedureStageDefinitionCommand>
{
    public async Task Handle(DeletePatientProcedureStageDefinitionCommand request, CancellationToken cancellationToken)
    {
        var definition = await repository.GetStageDefinitionByIdAsync(request.StageDefinitionId, cancellationToken)
                         ?? throw new NotFoundException($"Stage definition '{request.StageDefinitionId}' was not found.");

        var usage = (await repository.GetStageDefinitionUsageAsync([definition.Id], cancellationToken))
            .GetValueOrDefault(definition.Id, StageDefinitionUsage.None);

        if (usage.InUse)
            throw new ConflictException(
                $"The '{definition.Name}' stage is used by {usage.TemplateCount} template(s) and {usage.ProcedureCount} procedure(s), " +
                "so it can't be deleted. Archive it instead.");

        await repository.DeleteStageDefinitionAsync(definition.Id, cancellationToken);
    }
}
