using MedApp.Application.Procedures.Common;
using MedApp.Contracts.Appointments.Responses;
using MediatR;

namespace MedApp.Application.Appointments.Commands.BookAppointment;

/// <summary>
/// Books a Requested appointment, or moves a Booked one to another time or member of staff: in the future, inside the
/// working day, clash-free.
/// </summary>
public sealed record BookAppointmentCommand(
    Guid AppointmentId,
    Guid StaffUserId,
    DateTime StartUtc,
    DateTime EndUtc,
    ProcedureActor Actor)
    : IRequest<AppointmentResponse>;
