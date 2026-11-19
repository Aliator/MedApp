using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.Application.Pathways.Common;

/// <summary>
/// Pathways grow (the owner's model, MC10b): a template is the full tree of every possibility, and a patient's pathway
/// holds only the stages up to its next choice node not yet recorded (<see cref="GrownStageOrders"/>). Recording a choice
/// node's outcome adds that outcome's stages, up to the next choice node; an outcome not chosen never appears. Never
/// chosen by MedApp: the outcomes are the clinician's. The same rules as the light demo's <c>grownStageOrders</c>,
/// <c>grownLinks</c> and <c>growPathway</c> (<c>src/api/pathwayRoutes.ts</c>, <c>src/demo/seed.ts</c>).
/// </summary>
public static class PathwayGrowth
{
    /// <summary>Whether two outcome names are the same outcome: ignoring case and the spaces around them.</summary>
    public static bool SameOutcome(string? a, string? b) =>
        string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The template stages a pathway has, by order: the first, then every stage a taken link leads to, where a choice
    /// node's links are taken only for its recorded outcome. A stage waits while a stage that could still lead into it
    /// isn't settled: a choice node not yet recorded with a link to it, or a stage still reachable through one (so a
    /// join after a fork never starts before every route that may reach it is known). <paramref name="recorded"/> is each
    /// choice node's outcome, by its order, as the clinician recorded it.
    /// </summary>
    public static IReadOnlyList<int> GrownStageOrders(
        IReadOnlyList<PathwayPlanStage> stages,
        IReadOnlyList<PathwayTemplateLink> links,
        IReadOnlyDictionary<int, string> recorded)
    {
        if (stages.Count == 0)
            return [];

        var byOrder = stages.ToDictionary(s => s.TemplateStageOrder);
        var present = new HashSet<int> { byOrder.Keys.Min() };
        for (var changed = true; changed;)
        {
            changed = false;

            // Every stage that may yet be reached: through taken links, and through an unrecorded choice node's every link.
            var reachable = new HashSet<int>(present);
            var queue = new Queue<int>(present);
            while (queue.Count > 0)
            {
                var from = queue.Dequeue();
                var stage = byOrder.GetValueOrDefault(from);
                var isOpen = stage?.ChoiceKind is not null && !recorded.ContainsKey(from);
                foreach (var link in links)
                    if (link.FromStageOrder == from && !reachable.Contains(link.ToStageOrder)
                        && (isOpen || Takes(stage, link.ToStageOrder, recorded)))
                    {
                        reachable.Add(link.ToStageOrder);
                        queue.Enqueue(link.ToStageOrder);
                    }
            }

            foreach (var link in links)
            {
                var (from, to) = (link.FromStageOrder, link.ToStageOrder);
                if (present.Contains(to) || !present.Contains(from) || !Takes(byOrder.GetValueOrDefault(from), to, recorded))
                    continue;

                var waits = links.Any(other =>
                {
                    if (other.ToStageOrder != to)
                        return false;

                    var before = other.FromStageOrder;
                    return present.Contains(before)
                        ? byOrder.GetValueOrDefault(before)?.ChoiceKind is not null && !recorded.ContainsKey(before)
                        : reachable.Contains(before);
                });
                if (waits)
                    continue;

                present.Add(to);
                changed = true;
            }
        }

        return present.Order().ToList();
    }

    /// <summary>The links among a pathway's template stages: the template's, less those a recorded choice node's outcome didn't take.</summary>
    public static IReadOnlyList<PathwayTemplateLink> GrownLinks(
        IReadOnlyList<PathwayPlanStage> stages,
        IReadOnlyList<PathwayTemplateLink> links,
        IReadOnlyCollection<int> present,
        IReadOnlyDictionary<int, string> recorded)
    {
        var byOrder = stages.ToDictionary(s => s.TemplateStageOrder);
        var has = present.ToHashSet();
        return links
            .Where(l => has.Contains(l.FromStageOrder) && has.Contains(l.ToStageOrder)
                        && Takes(byOrder.GetValueOrDefault(l.FromStageOrder), l.ToStageOrder, recorded))
            .ToList();
    }

    /// <summary>Each recorded outcome on a pathway, by its choice node's template order.</summary>
    public static IReadOnlyDictionary<int, string> RecordedOutcomes(IEnumerable<PatientPathwayStage> stages) =>
        stages
            .Where(s => s.TemplateStageOrder is not null && !string.IsNullOrWhiteSpace(s.Outcome))
            .GroupBy(s => s.TemplateStageOrder!.Value)
            .ToDictionary(g => g.Key, g => g.First().Outcome!);

    /// <summary>
    /// A choice node's outcomes, in the template's order, each with the names of the stages recording it would add (up
    /// to the next choice node). None is ever chosen or pre-selected: the clinician records one. Empty for a plain stage
    /// or a pathway with no plan.
    /// </summary>
    public static IReadOnlyList<(string Name, IReadOnlyList<string> StageNames)> Outcomes(PatientPathway pathway, PatientPathwayStage stage)
    {
        if (stage.ChoiceKind is null || pathway.Plan is not { } plan
            || plan.Stages.FirstOrDefault(s => s.TemplateStageOrder == stage.TemplateStageOrder) is not { } planned)
            return [];

        var has = pathway.Stages.Select(s => s.TemplateStageOrder).OfType<int>().ToHashSet();
        var recorded = RecordedOutcomes(pathway.Stages).ToDictionary();
        return planned.Outcomes
            .Select(outcome =>
            {
                var withIt = new Dictionary<int, string>(recorded) { [planned.TemplateStageOrder] = outcome.Name };
                var added = GrownStageOrders(plan.Stages, plan.Links, withIt).Where(o => !has.Contains(o));
                return (outcome.Name, (IReadOnlyList<string>)added
                    .Select(o => plan.Stages.First(s => s.TemplateStageOrder == o).Name)
                    .ToList());
            })
            .ToList();
    }

    /// <summary>
    /// Grows a pathway from its plan: adds every template stage now reached (up to the next choice node not yet
    /// recorded), numbers its stages again (the template's in their template order, then any added with a branch), and
    /// links them as the template does, less the links an outcome didn't take (adding only: a link once made stays).
    /// Returns the stages and links added. Nothing for a pathway without a plan.
    /// </summary>
    public static (IReadOnlyList<PatientPathwayStage> Stages, IReadOnlyList<PatientPathwayStageLink> Links) Grow(
        PatientPathway pathway, DateTime nowUtc)
    {
        if (pathway.Plan is not { } plan)
            return ([], []);

        var recorded = RecordedOutcomes(pathway.Stages);
        var orders = GrownStageOrders(plan.Stages, plan.Links, recorded);
        var has = pathway.Stages.Select(s => s.TemplateStageOrder).OfType<int>().ToHashSet();
        var added = orders
            .Where(o => !has.Contains(o))
            .Select(o => NewStage(pathway, plan.Stages.First(s => s.TemplateStageOrder == o), nowUtc))
            .ToList();
        pathway.Stages.AddRange(added);

        var fromTemplate = pathway.Stages.Where(s => s.TemplateStageOrder is not null).OrderBy(s => s.TemplateStageOrder).ToList();
        var fromBranches = pathway.Stages.Where(s => s.TemplateStageOrder is null).OrderBy(s => s.StageOrder).ToList();
        var order = 0;
        foreach (var stage in fromTemplate.Concat(fromBranches))
        {
            order++;
            if (stage.StageOrder == order)
                continue;

            stage.StageOrder = order;
            stage.LastUpdated = nowUtc;
        }

        var idAt = fromTemplate.ToDictionary(s => s.TemplateStageOrder!.Value, s => s.Id);
        var newLinks = GrownLinks(plan.Stages, plan.Links, orders, recorded)
            .Select(l => new PatientPathwayStageLink
            {
                PatientPathwayId = pathway.Id,
                FromStageId = idAt[l.FromStageOrder],
                ToStageId = idAt[l.ToStageOrder]
            })
            .Where(l => !pathway.Links.Any(x => x.FromStageId == l.FromStageId && x.ToStageId == l.ToStageId))
            .ToList();
        pathway.Links.AddRange(newLinks);

        return (added, newLinks);
    }

    /// <summary>
    /// A pathway's stage copied from a planned one (its definition's details and the template's options), still to do.
    /// <paramref name="templateStageOrder"/> is its place in the template, or null for a stage added with a branch.
    /// </summary>
    public static PatientPathwayStage NewStage(
        PatientPathway pathway,
        PathwayPlanStage planned,
        DateTime nowUtc,
        int? stageOrder = null,
        bool fromTemplate = true,
        Guid? branchAdditionId = null,
        Guid? ownerTeamId = null) => new()
    {
        Id = Guid.NewGuid(),
        PatientPathwayId = pathway.Id,
        StageDefinitionId = planned.StageDefinitionId,
        StageOrder = stageOrder ?? 0,
        TemplateStageOrder = fromTemplate ? planned.TemplateStageOrder : null,
        Name = planned.Name,
        Description = planned.Description,
        Instructions = planned.Instructions,
        SnomedCode = planned.SnomedCode,
        SnomedTerm = planned.SnomedTerm,
        VisitRequirement = planned.VisitRequirement,
        NeedsLetterKind = planned.NeedsLetterKind,
        WaitsForResults = planned.WaitsForResults,
        NeedsConsent = planned.NeedsConsent,
        IsOptional = fromTemplate && planned.IsOptional,
        StopsClock = fromTemplate && planned.StopsClock,
        ChoiceKind = fromTemplate ? planned.ChoiceKind : null,
        OwnerTeamId = fromTemplate ? NotOwn(pathway, planned.OwnerTeamId) : NotOwn(pathway, ownerTeamId),
        BranchAdditionId = branchAdditionId,
        CreatedAt = nowUtc,
        LastUpdated = nowUtc
    };

    /// <summary>A stage's owner as stored: null for the pathway's own team.</summary>
    private static Guid? NotOwn(PatientPathway pathway, Guid? teamId) => teamId == pathway.TeamId ? null : teamId;

    /// <summary>Whether a link is one a choice node's recorded outcome takes (any link from a plain stage is taken).</summary>
    private static bool Takes(PathwayPlanStage? stage, int to, IReadOnlyDictionary<int, string> recorded)
    {
        if (stage?.ChoiceKind is null)
            return true;

        return recorded.TryGetValue(stage.TemplateStageOrder, out var outcome)
               && stage.Outcomes.Any(o => SameOutcome(o.Name, outcome) && o.NextStageOrders.Contains(to));
    }
}
