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
}
