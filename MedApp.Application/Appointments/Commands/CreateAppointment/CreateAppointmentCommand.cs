using MedApp.Application.Procedures.Common;
using MedApp.Contracts.Appointments.Responses;
using MediatR;

namespace MedApp.Application.Appointments.Commands.CreateAppointment;

/// <summary>
/// A new appointment for a patient, optionally for one of their procedures and one of its stages. With a member of
/// staff, a start and an end it's Booked (in the future, inside the working day, clash-free); with none of them it's
/// Requested, to be booked later.
/// </summary>
public sealed record CreateAppointmentCommand(
    Guid PatientId,
    Guid? ProcedureId,
    Guid? StageId,
    Guid? StaffUserId,
    DateTime? StartUtc,
    DateTime? EndUtc,
    string? Notes,
    ProcedureActor Actor)
    : IRequest<AppointmentResponse>
{
    /// <summary>A member of staff, a start and an end are all given: it's booked straight away.</summary>
    public bool IsBooking => StaffUserId is not null && StartUtc is not null && EndUtc is not null;

    /// <summary>None of a member of staff, a start or an end is given: it's Requested.</summary>
    public bool IsRequest => StaffUserId is null && StartUtc is null && EndUtc is null;
}
