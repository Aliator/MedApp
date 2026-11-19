using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.Application.Pathways.Common;

/// <summary>
/// Tree pathways (MC8, <c>docs/multi-clinic-plan.md</c>): a pathway's stages are linked into a directed graph with no
/// cycles (<see cref="PatientPathway.Links"/>). A stage with several successors is a fork (every route runs side by side)
/// or a choice node; one with several predecessors is a join, waiting until each is finished or skipped. A stage is
/// <b>current</b> when it's still to do and nothing before it, on any route into it, is (<see cref="CurrentStages"/>), as
/// the light demo's <c>currentStages</c> has it.
/// <para>
/// A pathway with no links (one made before them, or a single stage) is a chain by <see cref="PatientPathwayStage.StageOrder"/>,
/// so it behaves as before.
/// </para>
/// </summary>
public static class PathwayGraph
{
    /// <summary>The stages straight before a stage, in order: its links' froms (the stage before it, in a chain).</summary>
    public static IReadOnlyList<PatientPathwayStage> Predecessors(PatientPathway pathway, PatientPathwayStage stage)
    {
        if (pathway.Links.Count == 0)
            return pathway.Stages.Where(s => s.StageOrder < stage.StageOrder).OrderBy(s => s.StageOrder).TakeLast(1).ToList();

        var from = pathway.Links.Where(l => l.ToStageId == stage.Id).Select(l => l.FromStageId).ToHashSet();
        return pathway.Stages.Where(s => from.Contains(s.Id)).OrderBy(s => s.StageOrder).ToList();
    }

    /// <summary>Every stage before a stage, on any route into it (its ancestors).</summary>
    public static IReadOnlySet<Guid> Ancestors(PatientPathway pathway, PatientPathwayStage stage)
    {
        if (pathway.Links.Count == 0)
            return pathway.Stages.Where(s => s.StageOrder < stage.StageOrder).Select(s => s.Id).ToHashSet();

        var seen = new HashSet<Guid>();
        var queue = new Queue<Guid>([stage.Id]);
        while (queue.Count > 0)
        {
            var to = queue.Dequeue();
            foreach (var link in pathway.Links.Where(l => l.ToStageId == to))
                if (seen.Add(link.FromStageId))
                    queue.Enqueue(link.FromStageId);
        }

        return seen;
    }

    /// <summary>Whether nothing before a stage, on any route into it, is still to do.</summary>
    public static bool IsReached(PatientPathway pathway, PatientPathwayStage stage)
    {
        var before = Ancestors(pathway, stage);
        return !pathway.Stages.Any(s => s.Id != stage.Id && before.Contains(s.Id) && PathwayWorkflow.IsToDo(s));
    }

    /// <summary>
    /// The stages being worked, in order: each still to do (neither finished nor skipped), with nothing before it still
    /// to do. A chain has one; routes side by side have one each; none once every stage is finished or skipped.
    /// </summary>
    public static IReadOnlyList<PatientPathwayStage> CurrentStages(PatientPathway pathway) =>
        pathway.Stages
            .OrderBy(s => s.StageOrder)
            .Where(s => PathwayWorkflow.IsToDo(s) && IsReached(pathway, s))
            .ToList();

    /// <summary>Whether a stage is one of the current ones.</summary>
    public static bool IsCurrent(PatientPathway pathway, PatientPathwayStage stage) =>
        PathwayWorkflow.IsToDo(stage) && pathway.Stages.Any(s => s.Id == stage.Id) && IsReached(pathway, stage);

    /// <summary>The first stage still to do before a stage (on a route into it), by order: what has to be done first.</summary>
    public static PatientPathwayStage? FirstToDoBefore(PatientPathway pathway, PatientPathwayStage stage)
    {
        var before = Ancestors(pathway, stage);
        return pathway.Stages.Where(s => before.Contains(s.Id) && PathwayWorkflow.IsToDo(s)).MinBy(s => s.StageOrder);
    }

    /// <summary>The team that works a stage (MC7, the relay): its owner (the lab, for its stages), else the pathway's own.</summary>
    public static Guid StageTeam(PatientPathway pathway, PatientPathwayStage stage) => stage.OwnerTeamId ?? pathway.TeamId;

    /// <summary>The links of a template written as a list: each stage to the next.</summary>
    public static List<PathwayTemplateLink> ChainLinks(int stageCount) =>
        Enumerable.Range(1, Math.Max(0, stageCount - 1)).Select(o => new PathwayTemplateLink(o, o + 1)).ToList();
}
