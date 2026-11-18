using MedApp.Domain.Appointments;
using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.Application.Pathways.Common;

/// <summary>
/// Asks for the visits stages involve. When a stage with a visit becomes the current stage (the pathway is created,
/// or the stage before it is finished), a Requested appointment is made for it, unless it already has one that's
/// Requested, Booked or Attended (one booked ahead counts). None is made while the pathway is blocked or closed.
/// </summary>
public static class StageVisits
{
    /// <summary>
    /// After a change, asks for the current stage's visit if it needs one. <paramref name="appointments"/> are the
    /// stage's appointments as they stand (the change's new ones are counted too).
    /// </summary>
    /// <returns>The Requested appointment made, or null.</returns>
    public static Appointment? RequestForCurrentStage(PathwayChange change, IEnumerable<Appointment> appointments)
    {
        var pathway = change.Pathway;
        if (!PathwayWorkflow.IsOpen(pathway.Status))
            return null;

        var current = PathwayWorkflow.CurrentStage(pathway);
        if (current is null || current.VisitRequirement == VisitRequirement.None)
            return null;

        var hasOne = appointments.Concat(change.NewAppointments).Any(a =>
            a.StageId == current.Id
            && a.Status is AppointmentStatus.Requested or AppointmentStatus.Booked or AppointmentStatus.Attended);

        return hasOne ? null : change.RequestVisit(current);
    }

    /// <summary>
    /// After a stage's visit was withdrawn as entered in error, whether the stage should have its visit asked for again,
    /// by the same rules as when it became current (<see cref="RequestForCurrentStage"/>), and what to tell the person
    /// who withdrew it. <paramref name="appointments"/> are the stage's appointments as they stand, the withdrawn one
    /// included (already withdrawn). Null message: nothing to say (the stage involves no visit).
    /// <para>
    /// <c>FinishedWithoutVisit</c>: the stage is finished, requires a visit, and now has no attended one, so the
    /// pathway's assignees and the holders of FinishWithoutVisit are told (<c>AppointmentNotifications.AfterStageVisitWithdrawn</c>):
    /// they decide whether it stands finished without its visit or is unlocked (finishing it again won't ask for the visit).
    /// </para>
    /// </summary>
    public static (bool RequestAgain, string? Message, bool FinishedWithoutVisit) AfterVisitWithdrawn(
        PatientPathway pathway, PatientPathwayStage stage, IEnumerable<Appointment> appointments)
    {
        if (stage.VisitRequirement == VisitRequirement.None)
            return (false, null, false);

        var visits = appointments.Where(a => a.StageId == stage.Id).ToList();
        if (stage.IsCompleted)
        {
            // Not "finished with this visit": a walk-in can be recorded for a stage finished before it, on another
            // visit or by the admin bypass.
            if (visits.Any(a => a.Status == AppointmentStatus.Attended))
                return (false, $"The '{stage.Name}' stage is finished, and still has an attended visit.", false);

            var required = stage.VisitRequirement == VisitRequirement.Required;
            return (false,
                $"The '{stage.Name}' stage is finished, and now has no attended visit. If it still needs its visit, someone who can unlock stages can unlock it, and then book one." +
                (required ? " The pathway's assignees and those who can finish a stage without its visit are told." : ""),
                required);
        }

        if (!PathwayWorkflow.IsOpen(pathway.Status))
            return (false, $"No visit is asked for again while the pathway is {PathwayWorkflow.Describe(pathway.Status)}.", false);

        if (PathwayWorkflow.CurrentStage(pathway)?.Id != stage.Id)
            return (false, $"The '{stage.Name}' stage isn't the current stage, so no visit is asked for yet.", false);

        var other = visits.FirstOrDefault(a =>
            a.Status is AppointmentStatus.Requested or AppointmentStatus.Booked or AppointmentStatus.Attended);
        return other is null
            ? (true, $"The '{stage.Name}' stage's visit is back in Requires Appointment.", false)
            : (false, $"The '{stage.Name}' stage already has a visit that's {MedApp.Application.Appointments.Common.AppointmentWorkflow.Describe(other.Status)}, so no other is asked for.", false);
    }
}
