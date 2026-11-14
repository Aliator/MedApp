using MedApp.Application.Common.Exceptions;
using MedApp.Application.Procedures.Repositories;
using MedApp.Contracts.Procedures.PatientProcedureStages.Templates.Responses;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedureStages.Templates.Queries.GetPatientProcedureStageTemplateById;

public sealed class GetPatientProcedureTemplateByIdHandler(IPatientProcedureStagesRepository repository)
    : IRequestHandler<GetPatientProcedureTemplateByIdQuery, PatientProcedureStageTemplateResponse>
{
    public async Task<PatientProcedureStageTemplateResponse> Handle(
        GetPatientProcedureTemplateByIdQuery request,
        CancellationToken cancellationToken)
    {
        var template = await repository.GetStageTemplateByIdAsync(request.TemplateId, cancellationToken)
                       ?? throw new NotFoundException($"Template '{request.TemplateId}' was not found.");

        var counts = await repository.GetTemplateProcedureCountsAsync([template.Id], cancellationToken);
        return StageTemplateMapper.ToResponse(template, counts.GetValueOrDefault(template.Id));
    }
}
