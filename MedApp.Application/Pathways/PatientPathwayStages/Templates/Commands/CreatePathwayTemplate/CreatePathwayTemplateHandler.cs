using FluentValidation;
using FluentValidation.Results;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.Repositories;
using MedApp.Application.Teams.Common;
using MedApp.Application.Teams.Repositories;
using MedApp.Contracts.Pathways.PatientPathwayStages.Templates.Responses;
using MedApp.Domain.Pathways.PatientPathways;
using MediatR;

namespace MedApp.Application.Pathways.PatientPathwayStages.Templates.Commands.CreatePathwayTemplate;

/// <summary>
/// Creates a template: its stages with their options (owners: the template's own team or the lab, MC7; choice nodes,
/// MC10b), its routes (MC8: links checked by <see cref="TemplateGraphRules"/>, else a chain) and its addable branches
/// (MC10), each refused with the light demo's words (<see cref="TemplateShape"/>).
/// </summary>
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

        // A stage is owned by the template's own team or, in Stage 1, the lab (MC7).
        var lab = await teams.GetLabAsync(cancellationToken);
        Refuse(TemplateShape.WhyOptionsInvalid(request.StageOptions ?? [], team.Id, lab?.Id));

        var templateId = Guid.NewGuid();
        var maps = TemplateShape.ToMaps(templateId, team.Id, request.StageDefinitionIdsInOrder, request.ClockStopStageOrder, request.StageOptions);
        var links = TemplateShape.Links(maps, request.Links, null, out var linkProblem);
        Refuse(linkProblem);

        var branchDefinitions = await BranchDefinitionsAsync(repository, request.AddableBranches, cancellationToken);
        List<PathwayTemplateBranch>? branches = [];
        if (request.AddableBranches is not null)
        {
            branches = TemplateShape.Branches(
                request.AddableBranches, [], maps.Count, branchDefinitions.Keys.ToHashSet(), out var branchProblem);
            Refuse(branchProblem);
        }

        var template = new PathwayTemplate
        {
            Id = templateId,
            Name = name,
            TeamId = team.Id,
            Team = team,
            RetentionClass = request.RetentionClass is null
                ? RetentionClass.Standard
                : PathwayEnums.Parse<RetentionClass>(request.RetentionClass),
            Maps = maps,
            Links = links!,
            AddableBranches = branches!
        };

        await repository.AddPathwayTemplateAsync(template, cancellationToken);

        var named = definitions.Values.Concat(branchDefinitions.Values).DistinctBy(d => d.Id).ToDictionary(d => d.Id);
        return PathwayTemplateMapper.ToResponse(template, 0, named, TeamNames(lab));
    }

    /// <summary>The stage definitions addable branches name (those that exist), by ID.</summary>
    internal static async Task<IReadOnlyDictionary<Guid, PatientPathwayStageDefinition>> BranchDefinitionsAsync(
        IPatientPathwayStagesRepository repository, IReadOnlyList<TemplateBranchInput>? branches, CancellationToken ct) =>
        branches is null || !branches.Any(b => (b.StageDefinitionIds ?? []).Count > 0)
            ? new Dictionary<Guid, PatientPathwayStageDefinition>()
            : (await repository.GetStageDefinitionsByIdAsync(
                branches.SelectMany(b => b.StageDefinitionIds ?? []).Distinct().ToList(), ct)).ToDictionary(d => d.Id);

    /// <summary>The lab's name, for its stages' owner in the response.</summary>
    internal static IReadOnlyDictionary<Guid, string> TeamNames(MedApp.Domain.Teams.Team? lab) =>
        lab is null ? new Dictionary<Guid, string>() : new Dictionary<Guid, string> { [lab.Id] = lab.Name };

    /// <summary>A 400 on the field named, when there's a problem.</summary>
    internal static void Refuse((string Property, string Message)? problem)
    {
        if (problem is { } p)
            throw new ValidationException([new ValidationFailure(p.Property, p.Message)]);
    }
}
