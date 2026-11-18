using MedApp.Domain.Appointments;
using MedApp.Domain.Procedures.PatientProcedures;

namespace MedApp.Application.Procedures.Common;

/// <summary>
/// Asks for the visits stages involve. When a stage with a visit becomes the current stage (the procedure is created,
/// or the stage before it is finished), a Requested appointment is made for it, unless it already has one that's
/// Requested, Booked or Attended (one booked ahead counts). None is made while the procedure is blocked or closed.
/// </summary>
public static class StageVisits
{
    /// <summary>
    /// After a change, asks for the current stage's visit if it needs one. <paramref name="appointments"/> are the
    /// stage's appointments as they stand (the change's new ones are counted too).
    /// </summary>
    /// <returns>The Requested appointment made, or null.</returns>
    public static Appointment? RequestForCurrentStage(ProcedureChange change, IEnumerable<Appointment> appointments)
    {
        var procedure = change.Procedure;
        if (!ProcedureWorkflow.IsOpen(procedure.Status))
            return null;

        var current = ProcedureWorkflow.CurrentStage(procedure);
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
    /// included. Null message: nothing to say (the stage involves no visit).
    /// </summary>
    public static (bool RequestAgain, string? Message) AfterVisitWithdrawn(
        PatientProcedure procedure, PatientProcedureStage stage, IEnumerable<Appointment> appointments)
    {
        if (stage.VisitRequirement == VisitRequirement.None)
            return (false, null);

        if (stage.IsCompleted)
            return (false, $"The '{stage.Name}' stage was finished with this visit. If it still needs its visit, an admin can unlock it, and then book one.");

        if (!ProcedureWorkflow.IsOpen(procedure.Status))
            return (false, $"No visit is asked for again while the procedure is {ProcedureWorkflow.Describe(procedure.Status)}.");

        if (ProcedureWorkflow.CurrentStage(procedure)?.Id != stage.Id)
            return (false, $"The '{stage.Name}' stage isn't the current stage, so no visit is asked for yet.");

        var other = appointments.FirstOrDefault(a =>
            a.StageId == stage.Id
            && a.Status is AppointmentStatus.Requested or AppointmentStatus.Booked or AppointmentStatus.Attended);
        return other is null
            ? (true, $"The '{stage.Name}' stage's visit is back in Requires Appointment.")
            : (false, $"The '{stage.Name}' stage already has a visit that's {MedApp.Application.Appointments.Common.AppointmentWorkflow.Describe(other.Status)}, so no other is asked for.");
    }
}
