using MedApp.Application.Procedures.Repositories;
using MedApp.Contracts.Procedures.PatientProcedureStages.Definitions.Responses;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedureStages.Definitions.Queries.GetAllPatientProcedureStageDefinitions;

public sealed class GetAllPatientProcedureStageDefinitionsHandler(IPatientProcedureStagesRepository repository)
    : IRequestHandler<GetAllPatientProcedureStageDefinitionsQuery, IReadOnlyList<PatientProcedureStageDefinitionResponse>>
{
    public async Task<IReadOnlyList<PatientProcedureStageDefinitionResponse>> Handle(
        GetAllPatientProcedureStageDefinitionsQuery request,
        CancellationToken cancellationToken)
    {
        var stageDefinitions = await repository.GetAllStageDefinitionsAsync(cancellationToken);

        return stageDefinitions
            .Select(x => new PatientProcedureStageDefinitionResponse
            {
                Id = x.Id,
                Name = x.Name,
                Description = x.Description,
                Instructions = x.Instructions
            })
            .ToList();
    }
}