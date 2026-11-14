using MedApp.Application.Common.Exceptions;
using MedApp.Application.Procedures.Repositories;
using MedApp.Contracts.Procedures.PatientProcedureStages.Templates.Responses;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedureStages.Templates.Commands.SetPatientProcedureStageTemplateArchived;

public sealed class SetPatientProcedureTemplateArchivedHandler(IPatientProcedureStagesRepository repository)
    : IRequestHandler<SetPatientProcedureTemplateArchivedCommand, PatientProcedureStageTemplateResponse>
{
    public async Task<PatientProcedureStageTemplateResponse> Handle(
        SetPatientProcedureTemplateArchivedCommand request,
        CancellationToken cancellationToken)
    {
        var template = await repository.GetStageTemplateByIdAsync(request.TemplateId, cancellationToken)
                       ?? throw new NotFoundException($"Template '{request.TemplateId}' was not found.");

        if (template.IsArchived != request.IsArchived)
        {
            template.IsArchived = request.IsArchived;
            await repository.UpdateStageTemplateAsync(template, null, cancellationToken);
        }

        var counts = await repository.GetTemplateProcedureCountsAsync([template.Id], cancellationToken);
        return StageTemplateMapper.ToResponse(template, counts.GetValueOrDefault(template.Id));
    }
}
