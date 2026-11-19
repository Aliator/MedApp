using MedApp.Application.Pathways.Common;
using MedApp.Domain.Lab;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Specimens;

namespace MedApp.Application.Lab.Common;

/// <summary>
/// Lab stages in clinic pathways (MC16 of <c>docs/multi-clinic-plan.md</c>; the light demo's <c>labStageAfter</c>,
/// <c>openCaseOfStage</c>, <c>caseOfStage</c>): one case per lab stage at a time, which the stage's samples join.
/// </summary>
public static class LabStages
{
    /// <summary>Whether the lab works the stage (MC7's relay): its results come from its lab case.</summary>
    public static bool IsLabStage(PatientPathwayStage stage, Guid? labTeamId) =>
        labTeamId is { } lab && stage.OwnerTeamId == lab;

    /// <summary>
    /// The pathway's lab stage a sample taken at <paramref name="stageId"/> goes to: that stage if it's the lab's, else
    /// the nearest lab stage after it on its routes (a chain by order for a pathway without links); none without one.
    /// </summary>
    public static PatientPathwayStage? LabStageAfter(PatientPathway pathway, Guid? stageId, Guid? labTeamId)
    {
        if (stageId is not { } start || labTeamId is null)
            return null;

        var seen = new HashSet<Guid> { start };
        var queue = new Queue<Guid>([start]);
        while (queue.Count > 0)
        {
            var from = queue.Dequeue();
            var stage = pathway.Stages.FirstOrDefault(s => s.Id == from);
            if (stage is not null && IsLabStage(stage, labTeamId))
                return stage;

            foreach (var next in Successors(pathway, from))
                if (seen.Add(next))
                    queue.Enqueue(next);
        }

        return null;
    }

    /// <summary>A lab stage's open case (one at a time), if any.</summary>
    public static LabCase? OpenCaseOf(PatientPathwayStage stage) =>
        stage.LabCases.FirstOrDefault(c => c.Status == LabCaseStatus.Open);

    /// <summary>The case a lab stage shows: its open one, else its latest not entered in error.</summary>
    public static LabCase? CaseOf(PatientPathwayStage stage) =>
        OpenCaseOf(stage)
        ?? stage.LabCases
            .Where(c => c.Status != LabCaseStatus.EnteredInError)
            .OrderByDescending(c => c.OpenedAtUtc)
            .FirstOrDefault();

    /// <summary>
    /// The test a lab stage's case takes when it opens before its sample: one asked for on the pathway's samples taken
    /// for it and not yet in a case (none: the lab chooses it when the sample comes).
    /// </summary>
    public static Guid? TestFromSamples(PatientPathway pathway, PatientPathwayStage labStage, IEnumerable<Specimen> pathwaySamples, Guid labTeamId) =>
        pathwaySamples
            .Where(s => s.LabCaseId is null
                        && s.Status is SpecimenStatus.Taken or SpecimenStatus.Sent
                        && LabStageAfter(pathway, s.StageId, labTeamId)?.Id == labStage.Id)
            .OrderBy(s => s.TakenAtUtc)
            .Select(s => s.LabTestId)
            .FirstOrDefault(id => id is not null);

    /// <summary>The lab stages a change made current: each opens its case, unless it has one open (MC16).</summary>
    public static IReadOnlyList<PatientPathwayStage> ReachedLabStages(PathwayChange change, Guid labTeamId) =>
        change.ReachedStages.Where(s => IsLabStage(s, labTeamId) && OpenCaseOf(s) is null).Distinct().ToList();

    private static IEnumerable<Guid> Successors(PatientPathway pathway, Guid stageId)
    {
        if (pathway.Links.Count > 0)
            return pathway.Links.Where(l => l.FromStageId == stageId).Select(l => l.ToStageId);

        var stage = pathway.Stages.FirstOrDefault(s => s.Id == stageId);
        return stage is null
            ? []
            : pathway.Stages.Where(s => s.StageOrder > stage.StageOrder).OrderBy(s => s.StageOrder).Take(1).Select(s => s.Id);
    }
}
