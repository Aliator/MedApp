using MedApp.Application.Common.Exceptions;
using MedApp.Application.Procedures.Repositories;
using MedApp.Contracts.Procedures.PatientProcedureStages.Definitions.Responses;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedureStages.Definitions.Commands.SetPatientProcedureStageDefinitionArchived;

public sealed class SetPatientProcedureStageDefinitionArchivedHandler(IPatientProcedureStagesRepository repository)
    : IRequestHandler<SetPatientProcedureStageDefinitionArchivedCommand, PatientProcedureStageDefinitionResponse>
{
    public async Task<PatientProcedureStageDefinitionResponse> Handle(
        SetPatientProcedureStageDefinitionArchivedCommand request,
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
