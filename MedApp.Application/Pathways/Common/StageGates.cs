using MedApp.Application.Letters.Common;
using MedApp.Application.Specimens.Common;
using MedApp.Domain.Consent;
using MedApp.Domain.Letters;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Specimens;

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
/// every finish path that can meet one (<c>FinishPathwayStageHandler</c>, and the specimen gate also medapp-dev's demo
/// data). None of these has an override: each is met by the action it asks for, or by correcting the item.
/// <list type="bullet">
/// <item><b>A letter</b> (decision 105): a stage that needs a letter of a kind (<see cref="PatientPathwayStage.NeedsLetterKind"/>)
/// can't be finished until a letter of that kind on the pathway is approved and sent to every recipient (a withdrawn
/// one doesn't count).</item>
/// <item><b>The last stage and open specimens</b> (decision 89): the stage whose finish leaves none unfinished can't be
/// finished while any of the pathway's specimens is open (taken, sent or received), so a pathway can't be discharged
/// with a report still to come.</item>
/// <item><b>Results awaiting sign-off</b> (decision 95): a stage that waits for results
/// (<see cref="PatientPathwayStage.WaitsForResults"/>), and the last stage always, can't be finished while any of the
/// pathway's results awaits sign-off, so a pathway can't be discharged with a result nobody has acted on (hazard H-46).</item>
/// </list>
/// <item><b>Consent</b> (decision 146): a stage that needs consent (<see cref="PatientPathwayStage.NeedsConsent"/>) can't be
/// finished without a Valid consent record for it (withdrawn or entered in error doesn't count). MedApp checks only that
/// one is recorded; it never assesses the consent.</item>
/// </summary>
public static class StageGates
{
    /// <summary>
    /// Why the stage can't be finished now, or null if these gates allow it. <paramref name="openSpecimens"/> are the
    /// pathway's open specimens, with their types.
    /// </summary>
    public static string? WhyCantFinish(
        PatientPathway pathway,
        PatientPathwayStage stage,
        IReadOnlyCollection<Specimen> openSpecimens)
    {
        if (!IsLastToFinish(pathway, stage))
            return null;

        var open = openSpecimens.Where(s => SpecimenWorkflow.IsOpen(s.Status)).ToList();
        if (open.Count == 0)
            return null;

        var each = open
            .OrderBy(s => s.TakenAtUtc).ThenBy(s => s.Number)
            .Select(s => $"{s.Number} ({s.SpecimenType?.Name ?? "sample"}, {SpecimenWorkflow.Describe(s.Status)})");
        var count = open.Count == 1 ? "a sample is" : $"{open.Count} samples are";
        return $"The pathway's last stage can't be finished while {count} still with the lab: {string.Join("; ", each)}. " +
               "Record each one's report, or mark it rejected, lost or entered in error.";
    }

    /// <summary>
    /// Why the stage can't be finished while results await sign-off, or null if it can: it waits for results, or it's
    /// the last to finish, and some of <paramref name="awaitingResults"/> (the pathway's results awaiting sign-off) still
    /// await. Dates only: MedApp never reads a result.
    /// </summary>
    public static string? WhyResultsAwaiting(
        PatientPathway pathway,
        PatientPathwayStage stage,
        IReadOnlyCollection<Domain.Results.Result> awaitingResults) =>
        Results.Common.ResultWorkflow.WhyAwaitingResultsBlockFinish(
            awaitingResults, IsLastToFinish(pathway, stage), stage.WaitsForResults);

    /// <summary>
    /// Whether finishing this stage would leave none of the pathway's stages to do, on any route (MC8). A choice node
    /// never is: recording its outcome adds that outcome's stages (or ends the route, which leaves the others).
    /// </summary>
    /// <summary>
    /// MC16: a lab stage isn't finished by hand while its lab case is open (409, no override). Authorising the case's
    /// report puts its results on the stage; the lab then finishes the stage, which sends the pathway back.
    /// </summary>
    public static string? WhyLabCaseOpen(PatientPathwayStage stage, Guid? labTeamId) =>
        Lab.Common.LabStages.IsLabStage(stage, labTeamId) && Lab.Common.LabStages.OpenCaseOf(stage) is { } open
            ? LabCaseOpenMessage(open.Number)
            : null;

    public static string LabCaseOpenMessage(string number) =>
        $"Lab case {number} is still open: authorise its report first, then finish this stage.";

    public static bool IsLastToFinish(PatientPathway pathway, PatientPathwayStage stage) =>
        stage.ChoiceKind is null && pathway.Stages.All(s => s.Id == stage.Id || !PathwayWorkflow.IsToDo(s));

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

    public const string ConsentMissingMessage =
        "This stage needs the patient's consent recorded before it can be finished: record it from the pathway's Consent card.";

    /// <summary>
    /// Why the stage can't be finished for want of consent, or null if it needs none or has a Valid record.
    /// <paramref name="stageRecords"/> are the stage's consent records, in any status.
    /// </summary>
    public static string? WhyConsentMissing(PatientPathwayStage stage, IReadOnlyCollection<ConsentRecord> stageRecords)
    {
        if (!stage.NeedsConsent)
            return null;

        var forStage = stageRecords.Where(r => r.StageId == stage.Id).ToList();
        if (forStage.Any(r => r.Status == ConsentStatus.Valid))
            return null;

        return forStage.Any(r => r.Status == ConsentStatus.Withdrawn)
            ? ConsentMissingMessage + " The consent recorded for it has been withdrawn."
            : ConsentMissingMessage;
    }

    private static string Article(string words) => "aeiou".Contains(words[0]) ? "an" : "a";
}
