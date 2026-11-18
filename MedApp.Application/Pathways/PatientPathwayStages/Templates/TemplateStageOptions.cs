using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.Application.Pathways.PatientPathwayStages.Templates;

/// <summary>
/// One template stage's options, beside its stage definition: whether it's optional, whether it's a branch point, and
/// the branch it belongs to (null: it always runs). The default is a plain stage, as every template had before.
/// </summary>
public sealed record TemplateStageOptions(bool IsOptional = false, bool ChoosesBranch = false, string? Branch = null)
{
    public static readonly TemplateStageOptions Plain = new();

    /// <summary>The branch name trimmed, or null when blank.</summary>
    public string? BranchName => string.IsNullOrWhiteSpace(Branch) ? null : Branch.Trim();
}

/// <summary>
/// The checks on a template's optional stages and branches (docs/centre-phases-3-5-plan.md, decision 117), shared by
/// the create and update validators. They keep every pathway well formed: it always ends on a stage that runs, and a
/// branch point always has a real choice.
/// </summary>
public static class TemplateBranchRules
{
    public const string CountMismatchMessage = "Give one set of stage options for each stage, in the same order.";
    public const string LastStageMessage = "The last stage always runs: it can't be optional, a branch point or in a branch.";
    public const string BranchPointOptionsMessage = "A branch point always runs: it can't be optional or in a branch itself.";
    public const string BranchWithoutPointMessage = "A stage in a branch must come after a branch point.";
    public const string TooFewBranchesMessage = "A branch point needs at least two branches after it, before the next branch point or the end.";

    /// <summary>The options for each stage: those given, or plain stages when none are.</summary>
    public static IReadOnlyList<TemplateStageOptions> OrPlain(IReadOnlyList<TemplateStageOptions>? options, int stageCount) =>
        options ?? Enumerable.Repeat(TemplateStageOptions.Plain, stageCount).ToList();

    /// <summary>Why a template's stage options are invalid, or null if they're fine.</summary>
    public static string? WhyInvalid(IReadOnlyList<TemplateStageOptions> options)
    {
        if (options.Count == 0)
            return null;

        var last = options[^1];
        if (last.IsOptional || last.ChoosesBranch || last.BranchName is not null)
            return LastStageMessage;

        var seenBranchPoint = false;
        for (var i = 0; i < options.Count; i++)
        {
            var stage = options[i];
            if (stage.BranchName is { Length: > PathwayBranchLimits.MaxBranchLength })
                return $"A branch name must not exceed {PathwayBranchLimits.MaxBranchLength} characters.";

            if (stage.ChoosesBranch)
            {
                if (stage.IsOptional || stage.BranchName is not null)
                    return BranchPointOptionsMessage;

                var branches = options
                    .Skip(i + 1)
                    .TakeWhile(s => !s.ChoosesBranch)
                    .Select(s => s.BranchName)
                    .OfType<string>()
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count();
                if (branches < 2)
                    return TooFewBranchesMessage;

                seenBranchPoint = true;
            }
            else if (stage.BranchName is not null && !seenBranchPoint)
            {
                return BranchWithoutPointMessage;
            }
        }

        return null;
    }
}
