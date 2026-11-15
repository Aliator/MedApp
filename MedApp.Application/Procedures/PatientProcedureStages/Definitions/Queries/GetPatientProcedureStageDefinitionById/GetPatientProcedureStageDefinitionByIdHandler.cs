using MedApp.Application.Common.Exceptions;
using MedApp.Application.Procedures.Repositories;
using MedApp.Contracts.Procedures.PatientProcedureStages.Definitions.Responses;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedureStages.Definitions.Queries.GetPatientProcedureStageDefinitionById;

public sealed class GetPatientProcedureStageDefinitionByIdHandler(IPatientProcedureStagesRepository repository)
    : IRequestHandler<GetPatientProcedureStageDefinitionByIdQuery, PatientProcedureStageDefinitionResponse>
{
    public async Task<PatientProcedureStageDefinitionResponse> Handle(
        GetPatientProcedureStageDefinitionByIdQuery request,
        CancellationToken cancellationToken)
    {
        var definition = await repository.GetStageDefinitionByIdAsync(request.StageDefinitionId, cancellationToken)
                         ?? throw new NotFoundException($"Stage definition '{request.StageDefinitionId}' was not found.");

        return await StageDefinitionMapper.ToResponseAsync(definition, repository, cancellationToken);
    }
}
