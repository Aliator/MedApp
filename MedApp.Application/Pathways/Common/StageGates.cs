using MedApp.Application.Letters.Common;
using MedApp.Domain.Letters;
using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.Application.Pathways.Common;

/// <summary>
/// The stage options for letters: a stage definition (and each pathway's copy of it) may name the kind of letter the
/// stage needs (decision 105). "None" or nothing for none.
/// </summary>
public static class StageLetterKinds
{
    public const string None = "None";

    public static readonly string Message =
        $"Needs letter kind must be None or one of: {LetterKinds.Names}.";

    public static bool IsValid(string? value) =>
        value is null || value == None || PathwayEnums.TryParse<LetterKind>(value, out _);

    /// <summary>The kind, or null for none (null, empty or "None").</summary>
    public static LetterKind? Parse(string? value) =>
        string.IsNullOrWhiteSpace(value) || value == None ? null : PathwayEnums.Parse<LetterKind>(value);
}

/// <summary>
/// What a stage needs before it can be finished, beyond the visit rule (<see cref="PathwayWorkflow.WhyStageNeedsVisit"/>,
/// which has its override): the home of the stage gates (<c>docs/centre-phases-3-5-plan.md</c>, decision 73), called by
/// every finish path that can meet one (<c>FinishPathwayStageHandler</c>). None of these has an override: each is met
/// by the action it asks for.
/// <list type="bullet">
/// <item><b>A letter</b> (decision 105): a stage that needs a letter of a kind (<see cref="PatientPathwayStage.NeedsLetterKind"/>)
/// can't be finished until a letter of that kind on the pathway is approved and sent to every recipient (a withdrawn
/// one doesn't count).</item>
/// </list>
/// Specimens (PR 13) and results (PR 14) add their gates here.
/// </summary>
public static class StageGates
{
    /// <summary>
    /// Why the stage can't be finished for want of its letter, or null if it needs none or has it.
    /// <paramref name="pathwayLetters"/> are the pathway's letters with their recipients.
    /// </summary>
    public static string? WhyLetterMissing(PatientPathwayStage stage, IReadOnlyCollection<Letter> pathwayLetters)
    {
        if (stage.NeedsLetterKind is not { } kind)
            return null;

        var ofKind = pathwayLetters.Where(l => l.Kind == kind && l.Status != LetterStatus.Withdrawn).ToList();
        if (ofKind.Any(l => l.Status == LetterStatus.Approved && l.IsSentToEveryone))
            return null;

        var what = LetterWorkflow.KindInMessage(kind);
        var state = ofKind.Count == 0
            ? $"None has been written yet: write one from the pathway's Letters card."
            : ofKind.Any(l => l.Status == LetterStatus.Approved)
                ? "It's approved, but not yet sent to every recipient: send it, or record how it was sent."
                : ofKind.Any(l => l.Status == LetterStatus.AwaitingApproval)
                    ? "It's awaiting approval."
                    : "It's still a draft.";
        return $"This stage needs {Article(what)} {what}, approved and sent to every recipient, before it can be finished. {state}";
    }

    private static string Article(string words) => "aeiou".Contains(words[0]) ? "an" : "a";
}
