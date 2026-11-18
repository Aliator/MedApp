using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Appointments.Responses;
using MediatR;

namespace MedApp.Application.Appointments.Commands.MarkAppointmentEnteredInError;

/// <summary>
/// Withdraws an appointment recorded after the event (a walk-in) as entered in error: it didn't happen, or it's on the
/// wrong patient, pathway or stage. With a reason, by whoever recorded it within the 30 days it could be recorded
/// in, or by a holder of CorrectRecords, an admin (<paramref name="CanCorrectRecords"/>), at any time. It leaves the calendar and stops counting as a stage's
/// visit; its history keeps what was recorded. If it was a stage's visit, the stage's visit is asked for again when
/// the stage would ask for one and the record can change (<c>StageVisits.AfterVisitWithdrawn</c>), and the response
/// says so, or why not. A finished stage that requires a visit, left without one, is told to the pathway's
/// assignees and the admins (<c>StageVisitWithdrawn</c>).
/// </summary>
public sealed record MarkAppointmentEnteredInErrorCommand(Guid AppointmentId, string Reason, PathwayActor Actor, bool CanCorrectRecords)
    : IRequest<EnteredInErrorResponse>;
