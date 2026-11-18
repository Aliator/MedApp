using MedApp.Application.Common.Exceptions;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.Repositories;
using MedApp.Application.Teams.Common;
using MedApp.Application.Teams.Repositories;
using MedApp.Contracts.Pathways.PatientPathwayStages.Templates.Responses;
using MedApp.Domain.Pathways.PatientPathways;
using MediatR;

namespace MedApp.Application.Pathways.PatientPathwayStages.Templates.Commands.CreatePathwayTemplate;

public sealed class CreatePathwayTemplateHandler(
    IPatientPathwayStagesRepository repository,
    ITeamRepository teams)
    : IRequestHandler<CreatePathwayTemplateCommand, PathwayTemplateResponse>
{
    public async Task<PathwayTemplateResponse> Handle(
        CreatePathwayTemplateCommand request,
        CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (await repository.PathwayTemplateNameExistsAsync(name, null, cancellationToken))
            throw new ConflictException($"A template called '{name}' already exists.");

        var definitions = await TemplateStages.CheckAsync(
            repository, request.StageDefinitionIdsInOrder, new HashSet<Guid>(), cancellationToken);

        // An archived team can't be chosen (409); one that doesn't exist is a 404.
        var team = (await TeamRules.CheckChosenAsync(
            teams, [request.TeamId], new HashSet<Guid>(), cancellationToken))[request.TeamId];

        var templateId = Guid.NewGuid();
        var template = new PathwayTemplate
        {
            Id = templateId,
            Name = name,
            TeamId = team.Id,
            Team = team,
            RetentionClass = request.RetentionClass is null
                ? RetentionClass.Standard
                : PathwayEnums.Parse<RetentionClass>(request.RetentionClass),
            Maps = PathwayTemplateMapper.ToMaps(templateId, request.StageDefinitionIdsInOrder, request.ClockStopStageOrder, request.StageOptions)
        };

        await repository.AddPathwayTemplateAsync(template, cancellationToken);

        return PathwayTemplateMapper.ToResponse(template, 0, definitions);
    }
}
