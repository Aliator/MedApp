using MedApp.Application.Common.Exceptions;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.Repositories;
using MedApp.Application.Patients.Common;
using MedApp.Application.Teams.Common;
using MedApp.Application.Teams.Repositories;
using MedApp.Contracts.Pathways.PatientPathwayStages.Templates.Responses;
using MedApp.Domain.Pathways.PatientPathways;
using MediatR;
using Microsoft.Extensions.Options;

namespace MedApp.Application.Pathways.PatientPathwayStages.Templates.Commands.UpdatePathwayTemplate;

public sealed class UpdatePathwayTemplateHandler(
    IPatientPathwayStagesRepository repository,
    ITeamRepository teams,
    IOptions<RetentionOptions> retention)
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

        // Moving it: an archived team can only be kept, not newly chosen (409).
        if (request.TeamId is { } teamId && teamId != template.TeamId)
        {
            var team = (await TeamRules.CheckChosenAsync(
                teams, [teamId], new HashSet<Guid> { template.TeamId }, cancellationToken))[teamId];
            template.TeamId = team.Id;
            template.Team = team;
        }

        var classBefore = template.RetentionClass;
        if (request.RetentionClass is not null)
            template.RetentionClass = PathwayEnums.Parse<RetentionClass>(request.RetentionClass);

        IReadOnlyDictionary<Guid, PatientPathwayStageDefinition>? definitions = null;
        if (request.StageDefinitionIdsInOrder is { } ids)
        {
            var current = template.Maps.Select(m => m.StageDefinitionId).ToHashSet();
            definitions = await TemplateStages.CheckAsync(repository, ids, current, cancellationToken);
        }

        // A page from before stage options sends none: while the stage list is unchanged, the stored ones stay.
        var options = request.StageOptions ?? KeptOptions(template, request.StageDefinitionIdsInOrder);

        await repository.UpdatePathwayTemplateAsync(
            template, request.StageDefinitionIdsInOrder, request.ClockStopStageOrder, options, cancellationToken);

        // The class is about the kind of care, so a longer one covers the pathways already made from the template too
        // (a class set after go-live mustn't leave its old records on the shorter period). Never the other way: lowering
        // a template's class, even by mistake, mustn't bring existing records into the review early (PR 27's review).
        if (template.RetentionClass != classBefore)
        {
            var shorter = retention.Value.Classes.ShorterThan(template.RetentionClass);
            if (shorter.Count > 0)
                await repository.RaisePathwayRetentionClassesAsync(template.Id, template.RetentionClass, shorter, cancellationToken);
        }

        if (request.StageDefinitionIdsInOrder is { } newIds)
            template.Maps = PathwayTemplateMapper.ToMaps(template.Id, newIds, request.ClockStopStageOrder, options);

        var counts = await repository.GetTemplatePathwayCountsAsync([template.Id], cancellationToken);
        return PathwayTemplateMapper.ToResponse(template, counts.GetValueOrDefault(template.Id), definitions);
    }

    /// <summary>
    /// The template's stored stage options, when a new stage list is the same as its own (same definitions, same order);
    /// otherwise null (plain stages).
    /// </summary>
    public static IReadOnlyList<TemplateStageOptions>? KeptOptions(PathwayTemplate template, IReadOnlyList<Guid>? newIds)
    {
        var maps = template.Maps.OrderBy(m => m.StageOrder).ToList();
        return newIds is not null && maps.Select(m => m.StageDefinitionId).SequenceEqual(newIds)
            ? maps.Select(m => new TemplateStageOptions(m.IsOptional, m.ChoosesBranch, m.Branch)).ToList()
            : null;
    }
}
