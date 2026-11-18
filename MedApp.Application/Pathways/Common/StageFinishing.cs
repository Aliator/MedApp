using MedApp.Domain.Appointments;
using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.Application.Pathways.Common;

/// <summary>
/// What finishing or skipping a stage does, once it's allowed: shared by <c>FinishPathwayStageHandler</c>,
/// <c>SkipPathwayStageHandler</c> and medapp-dev's demo data, so all write the same audit entries and visit changes.
/// The handler checks first that the stage can be edited (or skipped), that a Required visit was attended (or that an
/// admin is finishing without it, with a reason), and that a branch point's branch is one of its branches.
/// </summary>
public static class StageFinishing
{
    /// <summary>
    /// The stage after this one still to do (neither finished nor skipped, nor in <paramref name="skipping"/>, the
    /// stages a branch choice is about to skip): it becomes current, and may need its visit asked for.
    /// </summary>
    public static PatientPathwayStage? NextStage(
        PatientPathway pathway, PatientPathwayStage stage, IReadOnlyCollection<PatientPathwayStage>? skipping = null) =>
        pathway.Stages
            .OrderBy(s => s.StageOrder)
            .FirstOrDefault(s => s.StageOrder > stage.StageOrder
                                 && PathwayWorkflow.IsToDo(s)
                                 && (skipping is null || !skipping.Contains(s)));

    /// <summary>
    /// The stages whose appointments finishing <paramref name="stage"/> (choosing <paramref name="branch"/>, at a
    /// branch point) touches: the stage itself, the stages the branch choice skips (their Requested visits are
    /// cancelled), and the stage that becomes current (its visit may be asked for).
    /// </summary>
    public static IReadOnlyList<Guid> StagesTouchedByFinish(PatientPathway pathway, PatientPathwayStage stage, string? branch)
    {
        IReadOnlyList<PatientPathwayStage> skipping = stage.ChoosesBranch && !stage.IsCompleted
            ? PathwayWorkflow.StagesSkippedByBranch(pathway, stage, branch)
            : [];
        var next = NextStage(pathway, stage, skipping.ToList());
        return new[] { stage }.Concat(skipping).Append(next).OfType<PatientPathwayStage>().Select(s => s.Id).Distinct().ToList();
    }

    /// <summary>
    /// Finishes the stage on its change: noting a finish without its visit (with <paramref name="reason"/>, trimmed,
    /// if any), choosing a branch point's <paramref name="branch"/> (skipping the others' stages), cancelling the
    /// Requested visits of the stage and of any stage it skipped (Booked ones stay: someone may need to tell the
    /// patient), and asking for the next stage's visit. <paramref name="appointments"/> are the appointments of the
    /// stages <see cref="StagesTouchedByFinish"/> names.
    /// </summary>
    /// <returns>How it was finished without its visit, or null if it wasn't.</returns>
    public static FinishedWithoutVisit? Finish(
        PathwayChange change,
        PatientPathwayStage stage,
        IReadOnlyList<Appointment> appointments,
        string? reason,
        string? branch = null)
    {
        var stageAppointments = appointments.Where(a => a.StageId == stage.Id).ToList();
        var withoutVisit = PathwayWorkflow.FinishesWithoutVisit(stage, stageAppointments)
            ? new FinishedWithoutVisit(stage.VisitRequirement, string.IsNullOrWhiteSpace(reason) ? null : reason.Trim())
            : null;

        var skipped = change.FinishStage(stage, withoutVisit, branch);

        foreach (var appointment in stageAppointments)
            change.CancelRequestedVisit(appointment, PathwayChange.StageFinishedReason);

        CancelVisitsOfSkipped(change, skipped, appointments);

        StageVisits.RequestForCurrentStage(change, appointments);
        return withoutVisit;
    }

    /// <summary>
    /// Skips an optional stage on its change, with the reason and detail: cancelling its Requested visits (as when a
    /// stage is finished: a skipped stage has no visit; Booked ones stay) and asking for the next stage's visit.
    /// <paramref name="appointments"/> are the skipped stage's and the next stage's appointments.
    /// </summary>
    public static void Skip(
        PathwayChange change,
        PatientPathwayStage stage,
        StageSkipReason skipReason,
        string? detail,
        IReadOnlyList<Appointment> appointments)
    {
        change.SkipStage(stage, skipReason, detail);
        CancelVisitsOfSkipped(change, [stage], appointments);
        StageVisits.RequestForCurrentStage(change, appointments);
    }

    private static void CancelVisitsOfSkipped(
        PathwayChange change, IReadOnlyCollection<PatientPathwayStage> skipped, IReadOnlyList<Appointment> appointments)
    {
        var ids = skipped.Select(s => s.Id).ToHashSet();
        foreach (var appointment in appointments.Where(a => a.StageId is { } id && ids.Contains(id)))
            change.CancelRequestedVisit(appointment, PathwayChange.StageSkippedReason);
    }
}
