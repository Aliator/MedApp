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
        var stageDefinition = await repository.GetStageDefinitionByIdAsync(request.StageDefinitionId, cancellationToken)
                              ?? throw new NotFoundException($"Patient procedure stage definition '{request.StageDefinitionId}' was not found.");

        return new PatientProcedureStageDefinitionResponse
        {
            Id = stageDefinition.Id,
            Name = stageDefinition.Name,
            Description = stageDefinition.Description,
            Instructions = stageDefinition.Instructions
        };
    }
}