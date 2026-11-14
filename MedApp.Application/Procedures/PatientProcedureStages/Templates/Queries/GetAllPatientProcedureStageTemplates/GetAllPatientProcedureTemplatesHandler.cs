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
        var templates = await repository.GetStageTemplatesAsync(request.IncludeArchived, cancellationToken);
        var counts = await repository.GetTemplateProcedureCountsAsync(templates.Select(t => t.Id), cancellationToken);

        return templates
            .Select(t => StageTemplateMapper.ToResponse(t, counts.GetValueOrDefault(t.Id)))
            .ToList();
    }
}
