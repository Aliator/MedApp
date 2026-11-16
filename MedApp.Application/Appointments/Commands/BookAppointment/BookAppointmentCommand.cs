using MedApp.Application.Procedures.Common;
using MedApp.Contracts.Appointments.Responses;
using MediatR;

namespace MedApp.Application.Appointments.Commands.BookAppointment;

/// <summary>
/// Books a Requested appointment, or moves a Booked one to another time or member of staff: in the future, inside the
/// working day, clash-free. <paramref name="ExpectedStatus"/> is the status the caller saw ("Requested" to book it,
/// "Booked" to move it); if it's changed since, it's a 409.
/// </summary>
public sealed record BookAppointmentCommand(
    Guid AppointmentId,
    Guid StaffUserId,
    DateTime StartUtc,
    DateTime EndUtc,
    string ExpectedStatus,
    ProcedureActor Actor)
    : IRequest<AppointmentResponse>;
