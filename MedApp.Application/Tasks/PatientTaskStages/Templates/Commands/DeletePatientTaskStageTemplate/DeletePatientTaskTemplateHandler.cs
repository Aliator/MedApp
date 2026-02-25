using MedApp.Application.Tasks.Repositories;
using MediatR;

namespace MedApp.Application.Tasks.PatientTaskStages.Templates.Commands.DeletePatientTaskStageTemplate;

public sealed class DeletePatientTaskTemplateHandler(IPatientTaskStagesRepository repository)
    : IRequestHandler<DeletePatientTaskTemplateCommand, bool>
{
    public async Task<bool> Handle(DeletePatientTaskTemplateCommand request, CancellationToken cancellationToken)
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