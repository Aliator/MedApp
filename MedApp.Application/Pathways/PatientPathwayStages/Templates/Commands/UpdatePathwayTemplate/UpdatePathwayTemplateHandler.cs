using MedApp.Application.Common.Exceptions;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.PatientPathwayStages.Templates.Commands.CreatePathwayTemplate;
using MedApp.Application.Pathways.Repositories;
using MedApp.Application.Patients.Common;
using MedApp.Application.Teams.Common;
using MedApp.Application.Teams.Repositories;
using MedApp.Contracts.Pathways.PatientPathwayStages.Templates.Responses;
using MedApp.Domain.Pathways.PatientPathways;
using MediatR;
using Microsoft.Extensions.Options;

namespace MedApp.Application.Pathways.PatientPathwayStages.Templates.Commands.UpdatePathwayTemplate;

/// <summary>
/// Edits a template, as the light demo's <c>saveTemplate</c> does: its name, team and class; its stages and options
/// (owners, choice nodes); its routes (links sent are checked by <see cref="TemplateGraphRules"/>; a template with routes
/// that sends none keeps them, and can't change its stages as a list; a list stays a chain); and its addable branches
/// (sent: replaced; else kept). Pathways already made keep their own copy (their plan).
/// </summary>
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

        var maps = template.Maps.OrderBy(m => m.StageOrder).ToList();
        var named = maps.Where(m => m.StageDefinition is not null).Select(m => m.StageDefinition)
            .DistinctBy(d => d.Id).ToDictionary(d => d.Id);
        if (request.StageDefinitionIdsInOrder is { } ids)
        {
            var current = maps.Select(m => m.StageDefinitionId).ToHashSet();
            foreach (var (id, definition) in await TemplateStages.CheckAsync(repository, ids, current, cancellationToken))
                named[id] = definition;
        }

        var newIds = request.StageDefinitionIdsInOrder ?? maps.Select(m => m.StageDefinitionId).ToList();

        // A page from before stage options sends none: while the stage list is unchanged, the stored ones stay.
        var options = request.StageOptions ?? KeptOptions(template, newIds);
        var lab = await teams.GetLabAsync(cancellationToken);
        CreatePathwayTemplateHandler.Refuse(TemplateShape.WhyOptionsInvalid(options ?? [], template.TeamId, lab?.Id));

        // The clock-stop stage goes with a stage list; without one it stays where it is.
        var clockStop = request.StageDefinitionIdsInOrder is null
            ? maps.FirstOrDefault(m => m.StopsClock)?.StageOrder
            : request.ClockStopStageOrder;
        var newMaps = TemplateShape.ToMaps(template.Id, template.TeamId, newIds, clockStop, options);
        var links = TemplateShape.Links(newMaps, request.Links, template, out var linkProblem);
        CreatePathwayTemplateHandler.Refuse(linkProblem);

        if (request.AddableBranches is not null)
        {
            var branchDefinitions = await CreatePathwayTemplateHandler.BranchDefinitionsAsync(
                repository, request.AddableBranches, cancellationToken);
            var branches = TemplateShape.Branches(
                request.AddableBranches, template.AddableBranches, newMaps.Count, branchDefinitions.Keys.ToHashSet(), out var branchProblem);
            CreatePathwayTemplateHandler.Refuse(branchProblem);
            template.AddableBranches = branches!;
        }

        template.Links = links!;
        await repository.UpdatePathwayTemplateAsync(template, newMaps, cancellationToken);

        // The class is about the kind of care, so a longer one covers the pathways already made from the template too
        // (a class set after go-live mustn't leave its old records on the shorter period). Never the other way: lowering
        // a template's class, even by mistake, mustn't bring existing records into the review early (PR 27's review).
        if (template.RetentionClass != classBefore)
        {
            var shorter = retention.Value.Classes.ShorterThan(template.RetentionClass);
            if (shorter.Count > 0)
                await repository.RaisePathwayRetentionClassesAsync(template.Id, template.RetentionClass, shorter, cancellationToken);
        }

        template.Maps = newMaps;
        foreach (var definition in (await PathwayTemplateMapper.BranchDefinitionsAsync(repository, [template], cancellationToken)).Values)
            named[definition.Id] = definition;

        var counts = await repository.GetTemplatePathwayCountsAsync([template.Id], cancellationToken);
        return PathwayTemplateMapper.ToResponse(
            template, counts.GetValueOrDefault(template.Id), named, CreatePathwayTemplateHandler.TeamNames(lab));
    }

    /// <summary>
    /// The template's stored stage options, when a new stage list is the same as its own (same definitions, same order);
    /// otherwise null (plain stages).
    /// </summary>
    public static IReadOnlyList<TemplateStageOptions>? KeptOptions(PathwayTemplate template, IReadOnlyList<Guid>? newIds)
    {
        var maps = template.Maps.OrderBy(m => m.StageOrder).ToList();
        return newIds is not null && maps.Select(m => m.StageDefinitionId).SequenceEqual(newIds)
            ? maps.Select(TemplateShape.OptionsOf).ToList()
            : null;
    }
}
