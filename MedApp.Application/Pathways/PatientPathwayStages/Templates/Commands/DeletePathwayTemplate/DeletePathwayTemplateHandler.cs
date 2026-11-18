using MedApp.Application.Common.Exceptions;
using MedApp.Application.Pathways.Repositories;
using MediatR;

namespace MedApp.Application.Pathways.PatientPathwayStages.Templates.Commands.DeletePathwayTemplate;

public sealed class DeletePathwayTemplateHandler(IPatientPathwayStagesRepository repository)
    : IRequestHandler<DeletePathwayTemplateCommand>
{
    public async Task Handle(DeletePathwayTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await repository.GetPathwayTemplateByIdAsync(request.TemplateId, cancellationToken)
                       ?? throw new NotFoundException($"Template '{request.TemplateId}' was not found.");

        var pathwayCount = (await repository.GetTemplatePathwayCountsAsync([template.Id], cancellationToken))
            .GetValueOrDefault(template.Id);

        if (pathwayCount > 0)
            throw new ConflictException(
                $"The '{template.Name}' template has been used for {pathwayCount} pathway(s), so it can't be deleted. Archive it instead.");

        await repository.DeletePathwayTemplateAsync(template.Id, cancellationToken);
    }
}
