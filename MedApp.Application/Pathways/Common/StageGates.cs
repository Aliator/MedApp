using MedApp.Application.Specimens.Common;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Specimens;

namespace MedApp.Application.Pathways.Common;

/// <summary>
/// What a stage needs before it can be finished, beyond the visit rule (<see cref="PathwayWorkflow.WhyStageNeedsVisit"/>,
/// which has its override): the home of the stage gates (<c>docs/centre-phases-3-5-plan.md</c>, decision 73), called by
/// every finish path (<c>FinishPathwayStageHandler</c> and medapp-dev's demo data). None of these has an override: each
/// is met by the action it asks for, or by correcting the item.
/// <list type="bullet">
/// <item><b>The last stage and open specimens</b> (decision 89): the stage whose finish leaves none unfinished can't be
/// finished while any of the pathway's specimens is open (taken, sent or received), so a pathway can't be discharged
/// with a report still to come.</item>
/// </list>
/// Results (PR 14) and letters (PR 16) add their gates here.
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
            .Select(s => $"{s.Number} ({s.SpecimenType?.Name ?? "specimen"}, {SpecimenWorkflow.Describe(s.Status)})");
        var count = open.Count == 1 ? "a specimen is" : $"{open.Count} specimens are";
        return $"The pathway's last stage can't be finished while {count} still with the lab: {string.Join("; ", each)}. " +
               "Record each one's report, or mark it rejected, lost or entered in error.";
    }

    /// <summary>Whether finishing this stage would leave none of the pathway's stages unfinished.</summary>
    public static bool IsLastToFinish(PatientPathway pathway, PatientPathwayStage stage) =>
        pathway.Stages.All(s => s.Id == stage.Id || s.IsCompleted);
}
