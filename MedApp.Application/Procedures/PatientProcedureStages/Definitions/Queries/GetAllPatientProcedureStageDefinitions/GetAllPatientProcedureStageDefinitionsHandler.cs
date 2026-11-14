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
        var definitions = await repository.GetStageDefinitionsAsync(request.IncludeArchived, cancellationToken);
        var usage = await repository.GetStageDefinitionUsageAsync(definitions.Select(d => d.Id), cancellationToken);

        return definitions
            .Select(d => StageDefinitionMapper.ToResponse(d, usage.GetValueOrDefault(d.Id, StageDefinitionUsage.None)))
            .ToList();
    }
}
