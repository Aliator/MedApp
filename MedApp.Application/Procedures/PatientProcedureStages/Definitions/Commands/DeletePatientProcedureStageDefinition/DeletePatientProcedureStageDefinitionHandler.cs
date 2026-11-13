using MedApp.Application.Procedures.Repositories;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedureStages.Definitions.Commands.DeletePatientProcedureStageDefinition;

public sealed class DeletePatientProcedureStageDefinitionHandler(IPatientProcedureStagesRepository repository)
    : IRequestHandler<DeletePatientProcedureStageDefinitionCommand, bool>
{
    public async Task<bool> Handle(DeletePatientProcedureStageDefinitionCommand request, CancellationToken cancellationToken)
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