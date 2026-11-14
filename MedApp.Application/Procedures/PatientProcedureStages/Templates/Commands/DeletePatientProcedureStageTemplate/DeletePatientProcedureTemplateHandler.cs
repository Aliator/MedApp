using MedApp.Application.Common.Exceptions;
using MedApp.Application.Procedures.Repositories;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedureStages.Templates.Commands.DeletePatientProcedureStageTemplate;

public sealed class DeletePatientProcedureTemplateHandler(IPatientProcedureStagesRepository repository)
    : IRequestHandler<DeletePatientProcedureTemplateCommand>
{
    public async Task Handle(DeletePatientProcedureTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await repository.GetStageTemplateByIdAsync(request.TemplateId, cancellationToken)
                       ?? throw new NotFoundException($"Template '{request.TemplateId}' was not found.");

        var procedureCount = (await repository.GetTemplateProcedureCountsAsync([template.Id], cancellationToken))
            .GetValueOrDefault(template.Id);

        if (procedureCount > 0)
            throw new ConflictException(
                $"The '{template.Name}' template has been used for {procedureCount} procedure(s), so it can't be deleted. Archive it instead.");

        await repository.DeleteStageTemplateAsync(template.Id, cancellationToken);
    }
}
