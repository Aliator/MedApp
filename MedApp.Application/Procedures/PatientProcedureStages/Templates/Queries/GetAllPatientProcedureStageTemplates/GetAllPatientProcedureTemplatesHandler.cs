using MedApp.Application.Procedures.Repositories;
using MedApp.Contracts.Procedures.PatientProcedureStages.Templates.Responses;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedureStages.Templates.Queries.GetAllPatientProcedureStageTemplates;

public sealed class GetAllPatientProcedureTemplatesHandler(IPatientProcedureStagesRepository repository)
    : IRequestHandler<GetAllPatientProcedureTemplatesQuery, IReadOnlyList<PatientProcedureStageTemplateResponse>>
{
    public async Task<IReadOnlyList<PatientProcedureStageTemplateResponse>> Handle(
        GetAllPatientProcedureTemplatesQuery request,
        CancellationToken cancellationToken)
    {
        var templates = await repository.GetAllStageTemplatesAsync(cancellationToken);

        return templates
            .OrderBy(x => x.Name)
            .Select(x => new PatientProcedureStageTemplateResponse
            {
                Id = x.Id,
                Name = x.Name,
                StageDefinitionIdsInOrder = x.Maps
                    .OrderBy(map => map.StageOrder)
                    .Select(map => map.StageDefinitionId)
                    .ToList()
            })
            .ToList();
    }
}