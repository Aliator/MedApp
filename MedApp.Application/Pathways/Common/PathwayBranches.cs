using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.Application.Pathways.Common;

/// <summary>
/// Branches added on new information (MC10, <c>docs/multi-clinic-plan.md</c>): only from the template's list (kept in
/// the pathway's plan), with a reason, never suggested. A branch's stages are a chain after the stage chosen, on a route
/// of their own that ends by itself; completing the pathway waits for it. The light demo's <c>afterStagesFor</c>.
/// </summary>
public static class PathwayBranches
{
    public const string ReasonRequiredMessage = "Say why the branch is being added.";
    public const string NotTheTemplatesMessage = "Choose one of the template\u2019s branches.";
    public const string ChooseAfterStageMessage = "Choose the stage it comes after.";

    /// <summary>Why a pathway's stages can't be worked (a 409), in the light demo's words.</summary>
    public const string OnHoldOrClosedMessage = "This pathway can’t be worked on while it’s on hold or closed.";

    public static string ReasonTooLongMessage => $"At most {PathwayBranchLimits.MaxBranchReasonLength} characters.";

    /// <summary>Why the branch can't go after that stage now (a 409).</summary>
    public static string CantGoThereMessage(string branch, string stage) =>
        $"The '{branch}' branch can\u2019t be added after the '{stage}' stage.";

    /// <summary>
    /// Where a template's addable branch can be added now: after a template stage it may follow (any when it names
    /// none), not skipped, while the pathway can be worked and has a stage still to do; nowhere once it's been added,
    /// unless it repeats.
    /// </summary>
    public static IReadOnlyList<PatientPathwayStage> AfterStagesFor(PatientPathway pathway, PathwayPlanBranch branch)
    {
        if (!PathwayWorkflow.CanWorkOnStages(pathway) || !pathway.Stages.Any(PathwayWorkflow.IsToDo))
            return [];

        if (!branch.IsRepeatable && pathway.BranchAdditions.Any(a => a.TemplateBranchId == branch.Id))
            return [];

        var allowed = branch.AttachAfterStageOrders;
        return pathway.Stages
            .OrderBy(s => s.StageOrder)
            .Where(s => s.TemplateStageOrder is { } order && !s.IsSkipped
                        && (allowed.Count == 0 || allowed.Contains(order)))
            .ToList();
    }
}
