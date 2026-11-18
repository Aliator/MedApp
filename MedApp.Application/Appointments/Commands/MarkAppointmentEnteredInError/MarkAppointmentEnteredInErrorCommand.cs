using MedApp.Application.Procedures.Common;
using MedApp.Contracts.Appointments.Responses;
using MediatR;

namespace MedApp.Application.Appointments.Commands.MarkAppointmentEnteredInError;

/// <summary>
/// Withdraws an appointment recorded after the event (a walk-in) as entered in error: it didn't happen, or it's on the
/// wrong patient, procedure or stage. By whoever recorded it, or an admin (<paramref name="IsAdmin"/>), with a reason,
/// within the 30 days it could be recorded in. It leaves the calendar and stops counting as a stage's visit; its
/// history keeps what was recorded. If it was a stage's visit, the stage's visit is asked for again when the stage
/// would ask for one (<c>StageVisits.AfterVisitWithdrawn</c>), and the response says so, or why not.
/// </summary>
public sealed record MarkAppointmentEnteredInErrorCommand(Guid AppointmentId, string Reason, ProcedureActor Actor, bool IsAdmin)
    : IRequest<EnteredInErrorResponse>;
