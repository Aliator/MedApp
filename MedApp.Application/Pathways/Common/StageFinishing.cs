using MedApp.Domain.Appointments;
using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.Application.Pathways.Common;

/// <summary>
/// What finishing a stage does, once it's allowed: shared by <c>FinishPathwayStageHandler</c> and medapp-dev's demo
/// data, so both write the same audit entries and visit changes. The handler checks first that the stage can be
/// edited, and that a Required visit was attended (or that an admin is finishing without it, with a reason).
/// </summary>
public static class StageFinishing
{
    /// <summary>The stage after this one that isn't finished: it becomes current, and may need its visit asked for.</summary>
    public static PatientPathwayStage? NextStage(PatientPathway pathway, PatientPathwayStage stage) =>
        pathway.Stages
            .OrderBy(s => s.StageOrder)
            .FirstOrDefault(s => s.StageOrder > stage.StageOrder && !s.IsCompleted);

    /// <summary>
    /// Finishes the stage on its change: noting a finish without its visit (with <paramref name="reason"/>, trimmed,
    /// if any), cancelling its Requested visits (Booked ones stay: someone may need to tell the patient), and asking
    /// for the next stage's visit. <paramref name="appointments"/> are the stage's and the next stage's appointments.
    /// </summary>
    /// <returns>How it was finished without its visit, or null if it wasn't.</returns>
    public static FinishedWithoutVisit? Finish(
        PathwayChange change,
        PatientPathwayStage stage,
        IReadOnlyList<Appointment> appointments,
        string? reason)
    {
        var stageAppointments = appointments.Where(a => a.StageId == stage.Id).ToList();
        var withoutVisit = PathwayWorkflow.FinishesWithoutVisit(stage, stageAppointments)
            ? new FinishedWithoutVisit(stage.VisitRequirement, string.IsNullOrWhiteSpace(reason) ? null : reason.Trim())
            : null;

        change.FinishStage(stage, withoutVisit);

        foreach (var appointment in stageAppointments)
            change.CancelRequestedVisit(appointment, PathwayChange.StageFinishedReason);

        StageVisits.RequestForCurrentStage(change, appointments);
        return withoutVisit;
    }
}
