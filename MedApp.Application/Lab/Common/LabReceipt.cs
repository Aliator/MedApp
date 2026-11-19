using MedApp.Application.Lab.Repositories;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Specimens.Common;
using MedApp.Application.Specimens.Repositories;
using MedApp.Domain.Lab;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Specimens;
using Microsoft.Extensions.Options;

namespace MedApp.Application.Lab.Common;

/// <summary>What receiving a sample did to lab cases: the case changed (if any), and the pathway change (picking its lab stage up).</summary>
public sealed record LabReceiptResult(LabCaseChange? CaseChange, bool IsNewCase, PathwayChange? PathwayChange);

/// <summary>
/// How a clinic sample the lab receives finds its lab case (MC15 and MC16; the light demo's <c>openCaseOnReceipt</c>):
/// on a pathway with a lab stage, it joins that stage's open case (setting its test if it had none), or opens the
/// stage's case; elsewhere it joins its stage's open case for the same test, or a new one opens. A sample without a
/// test, for a case without one, waits for the lab to choose the test (opening it by hand). The first sample received
/// into a lab stage's case picks the stage up. Changes are tracked, not saved: the handler saves them with its own.
/// </summary>
public sealed class LabReceipt(
    ILabCaseRepository labCases,
    ISpecimenRepository specimens,
    IOptions<SpecimenOptions> options)
{
    /// <summary>A new case number from the one sequence (H-73).</summary>
    public async Task<string> NextNumberAsync(DateTime nowUtc, CancellationToken ct) =>
        SpecimenNumbers.Format(options.Value.NumberPrefix, nowUtc, await specimens.NextNumberAsync(ct));

    /// <summary>
    /// Finds or opens the received sample's case. <paramref name="pathway"/> is the sample's pathway, tracked with its
    /// links and stages (null for a hospital sample, which brings its own case: MC18).
    /// </summary>
    public async Task<LabReceiptResult> OnReceivedAsync(
        Specimen sample, PatientPathway? pathway, Guid? labTeamId, PathwayActor actor, DateTime nowUtc, CancellationToken ct)
    {
        if (sample.Source != SpecimenSource.Clinic || sample.LabCaseId is not null || labTeamId is not { } lab)
            return new LabReceiptResult(null, false, null);

        var labStage = pathway is null ? null : LabStages.LabStageAfter(pathway, sample.StageId, lab);
        var stageIds = new[] { labStage?.Id, sample.StageId }.OfType<Guid>().Distinct().ToList();
        var open = stageIds.Count == 0 ? [] : await labCases.GetOpenForStagesAsync(stageIds, ct);

        var waiting = labStage is null ? null : open.FirstOrDefault(c => c.StageId == labStage.Id);
        if (waiting is not null)
        {
            var change = new LabCaseChange(waiting, pathway, actor, nowUtc);
            if (waiting.LabTestId is null && sample.LabTestId is { } testId && await labCases.GetTestAsync(testId, ct) is { } test)
                change.ChooseTest(test);
            if (waiting.LabTestId is null)
                return new LabReceiptResult(null, false, null);

            var pickedUp = JoinStageCase(change, [sample], pathway, actor, nowUtc);
            labCases.Track(change, isNew: false);
            return new LabReceiptResult(change, false, pickedUp);
        }

        if (sample.LabTestId is not { } labTestId)
            return new LabReceiptResult(null, false, null);

        // Elsewhere, its stage's open case for the same test takes it (no lab stage: the case keeps the sample's stage).
        if (labStage is null && open.FirstOrDefault(c => c.StageId == sample.StageId && c.LabTestId == labTestId) is { } same)
        {
            var join = new LabCaseChange(same, pathway, actor, nowUtc);
            sample.LabCaseId = same.Id;
            sample.LabCase = same;
            join.Moved();
            labCases.Track(join, isNew: false);
            return new LabReceiptResult(join, false, null);
        }

        var opened = LabCaseChange.Open(
            await NextNumberAsync(nowUtc, ct),
            labTestId,
            await labCases.GetTestAsync(labTestId, ct),
            sample.PatientId,
            lab,
            SpecimenSource.Clinic,
            sample.RequestingTeamId,
            null,
            null,
            pathway,
            labStage?.Id ?? sample.StageId,
            [sample],
            actor,
            nowUtc);
        labCases.Track(opened, isNew: true);
        labStage?.LabCases.Add(opened.Case);

        var pathwayChange = labStage is not null && pathway is not null ? PickUp(pathway, labStage, actor, nowUtc) : null;
        return new LabReceiptResult(opened, true, pathwayChange);
    }

    /// <summary>
    /// Samples join a lab stage's case waiting for them: the first received is its Received step, which picks the stage
    /// up (returned, to save with the case). Nothing for a case already received.
    /// </summary>
    public static PathwayChange? JoinStageCase(
        LabCaseChange change, IReadOnlyList<Specimen> samples, PatientPathway? pathway, PathwayActor actor, DateTime nowUtc)
    {
        var wasReceived = LabCaseRules.IsReceived(change.Case);
        change.Join(samples);
        if (wasReceived || pathway is null || change.Case.StageId is not { } stageId)
            return null;

        var stage = pathway.Stages.FirstOrDefault(s => s.Id == stageId);
        return stage is null ? null : PickUp(pathway, stage, actor, nowUtc);
    }

    /// <summary>Receiving a lab stage's first sample picks the stage up (MC16), when it's current and waiting to be.</summary>
    private static PathwayChange? PickUp(PatientPathway pathway, PatientPathwayStage stage, PathwayActor actor, DateTime nowUtc)
    {
        if (!PathwayWorkflow.IsCurrent(pathway, stage))
            return null;

        var change = new PathwayChange(pathway, actor, nowUtc);
        return change.PickUp(stage) ? change : null;
    }
}
