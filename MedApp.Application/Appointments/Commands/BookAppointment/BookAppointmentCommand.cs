using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Appointments.Responses;
using MediatR;

namespace MedApp.Application.Appointments.Commands.BookAppointment;

/// <summary>
/// Books a Requested appointment, or moves a Booked one to another time or member of staff: in the future, inside the
/// working day, clash-free. <paramref name="ExpectedStatus"/> is the status the caller saw ("Requested" to book it,
/// "Booked" to move it); if it's changed since, it's a 409. With <paramref name="ClinicSessionId"/> it goes into one of
/// that session's slots (its clinician, its room); with <paramref name="RoomId"/> alone, into that room. With
/// <paramref name="TellPatient"/>, moving a Booked one to another time or room texts or emails the patient
/// (<c>AppointmentMoved</c>), as they chose, when patient messages are on.
/// </summary>
public sealed record BookAppointmentCommand(
    Guid AppointmentId,
    Guid StaffUserId,
    DateTime StartUtc,
    DateTime EndUtc,
    string ExpectedStatus,
    PathwayActor Actor,
    Guid? ClinicSessionId = null,
    Guid? RoomId = null,
    bool TellPatient = false)
    : IRequest<AppointmentResponse>;
