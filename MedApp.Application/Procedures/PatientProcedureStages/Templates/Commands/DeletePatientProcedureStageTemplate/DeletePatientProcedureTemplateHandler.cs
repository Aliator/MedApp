using MedApp.Application.Procedures.Repositories;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedureStages.Templates.Commands.DeletePatientProcedureStageTemplate;

public sealed class DeletePatientProcedureTemplateHandler(IPatientProcedureStagesRepository repository)
    : IRequestHandler<DeletePatientProcedureTemplateCommand, bool>
{
    public async Task<bool> Handle(DeletePatientProcedureTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await repository.GetStageTemplateByIdAsync(request.TemplateId, cancellationToken);

        if (template is null)
        {
            return false;
        }

        await repository.DeleteStageTemplateAsync(request.TemplateId, cancellationToken);

        return true;
    }
}