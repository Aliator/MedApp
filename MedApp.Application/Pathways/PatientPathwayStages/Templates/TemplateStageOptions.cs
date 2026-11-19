using MedApp.Application.Pathways.Common;
using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.Application.Pathways.PatientPathwayStages.Templates;

/// <summary>
/// One template stage's options, beside its stage definition: whether it's optional, a choice node's kind (by name:
/// Assessment or LabResult) and outcomes (MC10b), and the team that works it (MC7: null for the template's own, or the
/// lab). The default is a plain stage of the template's own team, as every template had before.
/// </summary>
public sealed record TemplateStageOptions(
    bool IsOptional = false,
    string? ChoiceKind = null,
    IReadOnlyList<PathwayTemplateOutcome>? Outcomes = null,
    Guid? OwnerTeamId = null)
{
    public static readonly TemplateStageOptions Plain = new();
}

/// <summary>An addable branch as an admin sends it (MC10); its ID, when sent, keeps the one it had.</summary>
public sealed record TemplateBranchInput(
    Guid? Id,
    string? Name,
    IReadOnlyList<Guid>? StageDefinitionIds,
    IReadOnlyList<int>? AttachAfterStageOrders,
    bool IsRepeatable);

/// <summary>
/// What's wrong with a template's routes (<c>TemplateGraphRules</c> in the plan, MC9 and choice nodes): the light demo's
/// <c>templateGraphProblems</c> (<c>src/api/pathwayRoutes.ts</c>), word for word, so the editor says what a save would
/// be refused for. One start (only the first stage has nothing before it), links only forward (so no cycles, and every
/// stage is reached from the first), and a choice node (never optional, never an end) with at least two named outcomes,
/// every stage straight after it led to by one of them. Routes may end anywhere else (a referral, the end of care). The
/// last stage can't be optional or a choice node.
/// </summary>
public static class TemplateGraphRules
{
    public const string BackwardLinkMessage = "A stage can only come after a stage above it.";
    public const string NoPredecessorMessage = "Choose what it comes after: only the first stage starts the pathway.";
    public const string LastStageMessage = "The last stage ends a route: it can’t be optional or a choice node.";
    public const string ChoiceOptionalMessage = "A choice node always runs: it can’t be optional.";
    public const string NoOutcomesMessage = "A choice node needs at least two named outcomes, each leading to stages after it or ending the route.";
    public const string DeadEndChoiceMessage = "A choice node can’t end a route: make the stages its outcomes lead to come after it.";
    public const string UnnamedOutcomeMessage = "Name the outcome that leads to this stage: the clinician records one of the choice node’s outcomes.";
    public const string SameOutcomeNameMessage = "Each outcome of a choice node needs a different name.";
    public const string OutcomeNotAfterMessage = "An outcome can lead only to a stage that comes straight after its choice node.";
    public static readonly string OutcomeTooLongMessage = $"An outcome's name must not exceed {PathwayBranchLimits.MaxBranchLength} characters.";

    /// <summary>
    /// Each stage's first problem, by its 0-based index. <paramref name="stages"/> are each stage's options (whether it's
    /// optional, a choice node and its outcomes); <paramref name="links"/> are by 1-based order.
    /// </summary>
    public static SortedDictionary<int, string> Problems(
        IReadOnlyList<(bool IsOptional, StageChoiceKind? ChoiceKind, IReadOnlyList<PathwayTemplateOutcome> Outcomes)> stages,
        IReadOnlyList<PathwayTemplateLink> links)
    {
        var problems = new SortedDictionary<int, string>();
        void Say(int index, string message) => problems.TryAdd(index, message);

        var last = stages.Count;
        foreach (var link in links)
            if (link.FromStageOrder < 1 || link.ToStageOrder > last || link.FromStageOrder >= link.ToStageOrder)
                Say(Math.Max(0, Math.Min(link.ToStageOrder, last) - 1), BackwardLinkMessage);

        var forward = links
            .Where(l => l.FromStageOrder >= 1 && l.ToStageOrder <= last && l.FromStageOrder < l.ToStageOrder)
            .ToList();
        static bool Named(PathwayTemplateOutcome o) => !string.IsNullOrWhiteSpace(o.Name);

        for (var i = 0; i < stages.Count; i++)
        {
            var stage = stages[i];
            var order = i + 1;
            var after = forward.Where(l => l.FromStageOrder == order).Select(l => l.ToStageOrder).ToList();
            if (order == last && (stage.IsOptional || stage.ChoiceKind is not null))
                Say(i, LastStageMessage);
            if (order > 1 && !forward.Any(l => l.ToStageOrder == order))
                Say(i, NoPredecessorMessage);
            if (stage.ChoiceKind is null)
                continue;

            if (stage.IsOptional)
                Say(i, ChoiceOptionalMessage);
            if (order < last && after.Count == 0)
                Say(i, DeadEndChoiceMessage);

            var names = new HashSet<string>();
            foreach (var outcome in stage.Outcomes)
            {
                var name = (outcome.Name?.Trim() ?? string.Empty).ToLowerInvariant();
                if (name.Length > PathwayBranchLimits.MaxBranchLength)
                    Say(i, OutcomeTooLongMessage);
                if (!names.Add(name))
                    Say(i, SameOutcomeNameMessage);
                if (outcome.NextStageOrders.Any(next => !after.Contains(next)))
                    Say(i, OutcomeNotAfterMessage);
            }

            foreach (var next in after.Order())
                if (!stage.Outcomes.Any(o => Named(o) && o.NextStageOrders.Contains(next)))
                    Say(next - 1, UnnamedOutcomeMessage);

            // An outcome may end the route (the end of care, or a referral recorded there).
            if (stage.Outcomes.Count(Named) < 2)
                Say(i, NoOutcomesMessage);
        }

        return problems;
    }
}

/// <summary>
/// A template's shape, checked and built as the light demo's <c>saveTemplate</c> does (shared by the create and update
/// handlers): the stage options (owners, choice nodes), the routes (links) and the addable branches. Every refusal is a
/// 400 naming the field, with the demo's words.
/// </summary>
public static class TemplateShape
{
    public const string OwnerMessage = "A stage can be owned only by the template’s own team or the lab.";
    public const string ChoiceKindMessage = "A choice node is an assessment or a lab result.";
    public const string LastOptionalMessage = "The last stage can’t be optional.";
    public const string ChoiceNeedsLinksMessage = "A choice node’s outcomes are routes: send the template’s links.";
    public const string ParallelRoutesLockedMessage =
        "This template has routes (side by side, or a choice node's outcomes), so its stages can only be changed with their routes (links).";
    public const string BranchNameMessage = "Give each branch a name.";
    public static readonly string BranchNameTooLongMessage = $"A branch name must not exceed {PathwayBranchLimits.MaxBranchLength} characters.";
    public const string BranchNamesDifferMessage = "Each branch needs a different name.";
    public const string CountMismatchMessage = "Give one set of stage options for each stage, in the same order.";

    public static string BranchNeedsStagesMessage(string name) => $"Add at least one stage to the '{name}' branch.";
    public static string BranchStageMissingMessage(string name) => $"A stage in the '{name}' branch isn’t in the stage library.";
    public static string BranchAttachMessage(string name) => $"The '{name}' branch can follow only a stage before the last.";

    /// <summary>
    /// Why a stage list's options are invalid, or null: owners only the template's own team (<paramref name="teamId"/>)
    /// or the lab (<paramref name="labTeamId"/>), choice kinds by name. The property's name for a 400 comes with it.
    /// </summary>
    public static (string Property, string Message)? WhyOptionsInvalid(
        IReadOnlyList<TemplateStageOptions> options, Guid teamId, Guid? labTeamId)
    {
        if (options.Any(o => o.OwnerTeamId is { } owner && owner != teamId && owner != labTeamId))
            return ("StageOptions", OwnerMessage);

        return options.Any(o => !string.IsNullOrWhiteSpace(o.ChoiceKind) && !PathwayEnums.TryParse<StageChoiceKind>(o.ChoiceKind, out _))
            ? ("StageOptions", ChoiceKindMessage)
            : null;
    }

    /// <summary>
    /// Template rows for a stage list, numbered from 1 in the order given (repeats allowed), the one at
    /// <paramref name="clockStopStageOrder"/> (if any) stopping the waiting-time clock, each with its options: an owner
    /// that's the template's own team is stored as none; a plain stage keeps no outcomes; outcomes are trimmed, their
    /// stages each once, in order.
    /// </summary>
    public static List<PathwayTemplateMap> ToMaps(
        Guid templateId,
        Guid teamId,
        IEnumerable<Guid> stageDefinitionIdsInOrder,
        int? clockStopStageOrder,
        IReadOnlyList<TemplateStageOptions>? options) =>
        stageDefinitionIdsInOrder
            .Select((id, index) =>
            {
                var option = options is not null && index < options.Count ? options[index] : TemplateStageOptions.Plain;
                var kind = ChoiceKindOf(option);
                return new PathwayTemplateMap
                {
                    TemplateId = templateId,
                    StageDefinitionId = id,
                    StageOrder = index + 1,
                    StopsClock = clockStopStageOrder == index + 1,
                    IsOptional = option.IsOptional,
                    ChoiceKind = kind,
                    Outcomes = kind is null
                        ? []
                        : (option.Outcomes ?? [])
                            .Select(o => new PathwayTemplateOutcome(o.Name?.Trim() ?? string.Empty, o.NextStageOrders.Distinct().Order().ToList()))
                            .ToList(),
                    OwnerTeamId = option.OwnerTeamId == teamId ? null : option.OwnerTeamId
                };
            })
            .ToList();

    /// <summary>A stage's options as they stand in a template (for a stage list sent again unchanged).</summary>
    public static TemplateStageOptions OptionsOf(PathwayTemplateMap map) =>
        new(map.IsOptional, map.ChoiceKind?.ToString(), map.Outcomes, map.OwnerTeamId);

    /// <summary>
    /// The links for a template's stages, or a 400 (<paramref name="problem"/>): routes sent are checked by
    /// <see cref="TemplateGraphRules"/> ("Stage N: …", on <c>links</c>); a template that has routes and sends none keeps
    /// them, its stages unchanged but for what doesn't change its routes (its owners); a list (no routes) is a chain, its
    /// last stage not optional and with no choice node.
    /// </summary>
    public static List<PathwayTemplateLink>? Links(
        IReadOnlyList<PathwayTemplateMap> maps,
        IReadOnlyList<PathwayTemplateLink>? sent,
        PathwayTemplate? existing,
        out (string Property, string Message)? problem)
    {
        problem = null;
        if (sent is not null)
        {
            var problems = TemplateGraphRules.Problems(
                maps.Select(m => (m.IsOptional, m.ChoiceKind, (IReadOnlyList<PathwayTemplateOutcome>)m.Outcomes)).ToList(), sent);
            if (problems.Count > 0)
            {
                var (index, message) = problems.First();
                problem = ("Links", $"Stage {index + 1}: {message}");
                return null;
            }

            return sent.Distinct().ToList();
        }

        if (existing is not null && HasRoutes(existing))
        {
            if (!SameRoutes(existing.Maps, maps))
            {
                problem = ("StageDefinitionIdsInOrder", ParallelRoutesLockedMessage);
                return null;
            }

            return existing.Links.ToList();
        }

        if (maps.Count > 0 && maps[^1].IsOptional)
        {
            problem = ("StageOptions", LastOptionalMessage);
            return null;
        }

        if (maps.Any(m => m.ChoiceKind is not null))
        {
            problem = ("Links", ChoiceNeedsLinksMessage);
            return null;
        }

        return PathwayGraph.ChainLinks(maps.Count);
    }

    /// <summary>Whether a template is more than a list: a choice node (whose outcomes are routes), or links that aren't a chain.</summary>
    public static bool HasRoutes(PathwayTemplate template)
    {
        if (template.Maps.Any(m => m.ChoiceKind is not null))
            return true;
        if (template.Links.Count == 0)
            return false;

        var chain = PathwayGraph.ChainLinks(template.Maps.Count).ToHashSet();
        return template.Links.Count != chain.Count || template.Links.Any(l => !chain.Contains(l));
    }

    /// <summary>
    /// A template's addable branches as saved, or a 400 on <c>addableBranches</c> (<paramref name="problem"/>): each
    /// named (unique, ignoring case, at most 50 characters), with at least one stage from the library
    /// (<paramref name="definitions"/>), following only stages before the last. A branch keeps its ID when it's sent
    /// back with one it had.
    /// </summary>
    public static List<PathwayTemplateBranch>? Branches(
        IReadOnlyList<TemplateBranchInput> sent,
        IReadOnlyList<PathwayTemplateBranch> existing,
        int stageCount,
        IReadOnlySet<Guid> definitions,
        out (string Property, string Message)? problem)
    {
        problem = null;
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<PathwayTemplateBranch>();
        foreach (var branch in sent)
        {
            var name = branch.Name?.Trim() ?? string.Empty;
            string? why = name.Length == 0 ? BranchNameMessage
                : name.Length > PathwayBranchLimits.MaxBranchLength ? BranchNameTooLongMessage
                : !names.Add(name) ? BranchNamesDifferMessage
                : (branch.StageDefinitionIds ?? []).Count == 0 ? BranchNeedsStagesMessage(name)
                : branch.StageDefinitionIds!.Any(id => !definitions.Contains(id)) ? BranchStageMissingMessage(name)
                : (branch.AttachAfterStageOrders ?? []).Any(o => o < 1 || o >= stageCount) ? BranchAttachMessage(name)
                : null;
            if (why is not null)
            {
                problem = ("AddableBranches", why);
                return null;
            }

            var kept = existing.FirstOrDefault(b => b.Id == branch.Id);
            result.Add(new PathwayTemplateBranch(
                kept?.Id ?? Guid.NewGuid(),
                name,
                branch.StageDefinitionIds!.ToList(),
                (branch.AttachAfterStageOrders ?? []).Distinct().Order().ToList(),
                branch.IsRepeatable));
        }

        return result;
    }

    private static StageChoiceKind? ChoiceKindOf(TemplateStageOptions option) =>
        PathwayEnums.TryParse<StageChoiceKind>(option.ChoiceKind, out var kind) ? kind : null;

    /// <summary>Whether two stage lists have the same routes: the same definitions, optional stages, choice nodes and outcomes.</summary>
    private static bool SameRoutes(IEnumerable<PathwayTemplateMap> before, IEnumerable<PathwayTemplateMap> after)
    {
        static string Key(PathwayTemplateMap m) =>
            $"{m.StageDefinitionId}|{m.IsOptional}|{m.ChoiceKind}|{string.Join(";", m.Outcomes.Select(o => $"{o.Name}>{string.Join(",", o.NextStageOrders)}"))}";

        return before.OrderBy(m => m.StageOrder).Select(Key).SequenceEqual(after.OrderBy(m => m.StageOrder).Select(Key));
    }
}
