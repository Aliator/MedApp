using MedApp.Application.Common.Exceptions;
using MedApp.Application.Pathways.Repositories;
using MedApp.Contracts.Pathways.PatientPathwayStages.Templates.Responses;
using MedApp.Domain.Pathways.PatientPathways;
using MediatR;

namespace MedApp.Application.Pathways.PatientPathwayStages.Templates.Commands.UpdatePathwayTemplate;

public sealed class UpdatePathwayTemplateHandler(IPatientPathwayStagesRepository repository)
    : IRequestHandler<UpdatePathwayTemplateCommand, PathwayTemplateResponse>
{
    public async Task<PathwayTemplateResponse> Handle(
        UpdatePathwayTemplateCommand request,
        CancellationToken cancellationToken)
    {
        var template = await repository.GetPathwayTemplateByIdAsync(request.TemplateId, cancellationToken)
                       ?? throw new NotFoundException($"Template '{request.TemplateId}' was not found.");

        if (request.Name?.Trim() is { } name && name != template.Name)
        {
            if (await repository.PathwayTemplateNameExistsAsync(name, template.Id, cancellationToken))
                throw new ConflictException($"A template called '{name}' already exists.");

            template.Name = name;
        }

        IReadOnlyDictionary<Guid, PatientPathwayStageDefinition>? definitions = null;
        if (request.StageDefinitionIdsInOrder is { } ids)
        {
            var current = template.Maps.Select(m => m.StageDefinitionId).ToHashSet();
            definitions = await TemplateStages.CheckAsync(repository, ids, current, cancellationToken);
        }

        await repository.UpdatePathwayTemplateAsync(template, request.StageDefinitionIdsInOrder, cancellationToken);

        if (request.StageDefinitionIdsInOrder is { } newIds)
            template.Maps = PathwayTemplateMapper.ToMaps(template.Id, newIds);

        var counts = await repository.GetTemplatePathwayCountsAsync([template.Id], cancellationToken);
        return PathwayTemplateMapper.ToResponse(template, counts.GetValueOrDefault(template.Id), definitions);
    }
}
