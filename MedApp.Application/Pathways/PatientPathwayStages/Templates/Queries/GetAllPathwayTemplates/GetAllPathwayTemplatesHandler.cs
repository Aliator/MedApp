using MedApp.Application.Pathways.Repositories;
using MedApp.Contracts.Pathways.PatientPathwayStages.Templates.Responses;
using MediatR;

namespace MedApp.Application.Pathways.PatientPathwayStages.Templates.Queries.GetAllPathwayTemplates;

public sealed class GetAllPathwayTemplatesHandler(IPatientPathwayStagesRepository repository)
    : IRequestHandler<GetAllPathwayTemplatesQuery, IReadOnlyList<PathwayTemplateResponse>>
{
    public async Task<IReadOnlyList<PathwayTemplateResponse>> Handle(
        GetAllPathwayTemplatesQuery request,
        CancellationToken cancellationToken)
    {
        var templates = await repository.GetPathwayTemplatesAsync(request.IncludeArchived, cancellationToken);
        var counts = await repository.GetTemplatePathwayCountsAsync(templates.Select(t => t.Id), cancellationToken);
        // Their addable branches' stages are named from the library (MC10).
        var definitions = await PathwayTemplateMapper.BranchDefinitionsAsync(repository, templates, cancellationToken);

        return templates
            .Select(t => PathwayTemplateMapper.ToResponse(t, counts.GetValueOrDefault(t.Id), definitions))
            .ToList();
    }
}
