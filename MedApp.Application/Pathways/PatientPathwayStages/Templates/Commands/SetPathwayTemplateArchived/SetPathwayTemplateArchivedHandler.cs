using MedApp.Application.Common.Exceptions;
using MedApp.Application.Pathways.Repositories;
using MedApp.Contracts.Pathways.PatientPathwayStages.Templates.Responses;
using MediatR;

namespace MedApp.Application.Pathways.PatientPathwayStages.Templates.Commands.SetPathwayTemplateArchived;

public sealed class SetPathwayTemplateArchivedHandler(IPatientPathwayStagesRepository repository)
    : IRequestHandler<SetPathwayTemplateArchivedCommand, PathwayTemplateResponse>
{
    public async Task<PathwayTemplateResponse> Handle(
        SetPathwayTemplateArchivedCommand request,
        CancellationToken cancellationToken)
    {
        var template = await repository.GetPathwayTemplateByIdAsync(request.TemplateId, cancellationToken)
                       ?? throw new NotFoundException($"Template '{request.TemplateId}' was not found.");

        if (template.IsArchived != request.IsArchived)
        {
            template.IsArchived = request.IsArchived;
            await repository.UpdatePathwayTemplateAsync(template, null, null, cancellationToken);
        }

        var counts = await repository.GetTemplatePathwayCountsAsync([template.Id], cancellationToken);
        return PathwayTemplateMapper.ToResponse(template, counts.GetValueOrDefault(template.Id));
    }
}
