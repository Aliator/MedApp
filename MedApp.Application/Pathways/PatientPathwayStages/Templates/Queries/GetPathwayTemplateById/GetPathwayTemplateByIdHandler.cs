using MedApp.Application.Common.Exceptions;
using MedApp.Application.Pathways.Repositories;
using MedApp.Contracts.Pathways.PatientPathwayStages.Templates.Responses;
using MediatR;

namespace MedApp.Application.Pathways.PatientPathwayStages.Templates.Queries.GetPathwayTemplateById;

public sealed class GetPathwayTemplateByIdHandler(IPatientPathwayStagesRepository repository)
    : IRequestHandler<GetPathwayTemplateByIdQuery, PathwayTemplateResponse>
{
    public async Task<PathwayTemplateResponse> Handle(
        GetPathwayTemplateByIdQuery request,
        CancellationToken cancellationToken)
    {
        var template = await repository.GetPathwayTemplateByIdAsync(request.TemplateId, cancellationToken)
                       ?? throw new NotFoundException($"Template '{request.TemplateId}' was not found.");

        var counts = await repository.GetTemplatePathwayCountsAsync([template.Id], cancellationToken);
        return PathwayTemplateMapper.ToResponse(template, counts.GetValueOrDefault(template.Id));
    }
}
