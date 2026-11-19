using MedApp.Domain.Appointments;
using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.Application.Pathways.Common;

/// <summary>
/// What finishing or skipping a stage does, once it's allowed: shared by <c>FinishPathwayStageHandler</c>,
/// <c>SkipPathwayStageHandler</c> and medapp-dev's demo data, so all write the same audit entries and visit changes.
/// The handler checks first that the stage can be edited (or skipped), that a Required visit was attended (or that an
/// admin is finishing without it, with a reason), and that a choice node's outcome is one of its outcomes.
/// </summary>
public static class StageFinishing
{
    /// <summary>
    /// The stages whose appointments finishing or skipping <paramref name="stage"/> touches: the stage itself, and every
    /// stage still to do (any of them may become current, on any route, and need its visit asked for). Stages a choice
    /// node's outcome adds are new, so have none.
    /// </summary>
    public static IReadOnlyList<Guid> StagesTouchedByFinish(PatientPathway pathway, PatientPathwayStage stage, string? outcome = null) =>
        new[] { stage }
            .Concat(pathway.Stages.Where(PathwayWorkflow.IsToDo))
            .Select(s => s.Id)
            .Distinct()
            .ToList();

    /// <summary>
    /// Finishes the stage on its change: noting a finish without its visit (with <paramref name="reason"/>, trimmed,
    /// if any), recording a choice node's <paramref name="outcome"/> (growing the pathway by its stages), cancelling the
    /// stage's Requested visits (Booked ones stay: someone may need to tell the patient), and asking for the visits of
    /// the stages it makes current. <paramref name="appointments"/> are the appointments of the stages
    /// <see cref="StagesTouchedByFinish"/> names.
    /// </summary>
    /// <returns>How it was finished without its visit, or null if it wasn't.</returns>
    public static FinishedWithoutVisit? Finish(
        PathwayChange change,
        PatientPathwayStage stage,
        IReadOnlyList<Appointment> appointments,
        string? reason,
        string? outcome = null)
    {
        var stageAppointments = appointments.Where(a => a.StageId == stage.Id).ToList();
        var withoutVisit = PathwayWorkflow.FinishesWithoutVisit(stage, stageAppointments)
            ? new FinishedWithoutVisit(stage.VisitRequirement, string.IsNullOrWhiteSpace(reason) ? null : reason.Trim())
            : null;

        change.FinishStage(stage, withoutVisit, outcome);

        foreach (var appointment in stageAppointments)
            change.CancelRequestedVisit(appointment, PathwayChange.StageFinishedReason);

        StageVisits.RequestForCurrentStage(change, appointments);
        return withoutVisit;
    }

    /// <summary>
    /// Skips an optional stage on its change, with the reason and detail: cancelling its Requested visits (as when a
    /// stage is finished: a skipped stage has no visit; Booked ones stay) and asking for the visits of the stages it
    /// makes current. <paramref name="appointments"/> are the skipped stage's and the stages still to do's appointments.
    /// </summary>
    public static void Skip(
        PathwayChange change,
        PatientPathwayStage stage,
        StageSkipReason skipReason,
        string? detail,
        IReadOnlyList<Appointment> appointments)
    {
        change.SkipStage(stage, skipReason, detail);
        foreach (var appointment in appointments.Where(a => a.StageId == stage.Id))
            change.CancelRequestedVisit(appointment, PathwayChange.StageSkippedReason);
        StageVisits.RequestForCurrentStage(change, appointments);
    }
}
