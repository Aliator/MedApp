using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Appointments.Responses;
using MediatR;

namespace MedApp.Application.Appointments.Commands.RecordPastAppointment;

/// <summary>
/// Records an appointment that has already happened (a walk-in, or one booked outside MedApp) with its outcome,
/// Attended or DidNotAttend (<paramref name="Outcome"/>): for a patient, optionally for one of their pathways and one
/// of its stages, with a member of staff, a start and an end. The start must have passed, on a working day within the
/// last 30 days, inside the working day, and the time mustn't overlap another of the member of staff's appointments
/// (blocks and meetings may overlap it: what happened, happened). A stage's visit in Requires Appointment becomes the recorded
/// one, so the stage keeps one appointment per visit.
/// </summary>
public sealed record RecordPastAppointmentCommand(
    Guid PatientId,
    Guid? PathwayId,
    Guid? StageId,
    Guid StaffUserId,
    DateTime StartUtc,
    DateTime EndUtc,
    string Outcome,
    string? Notes,
    PathwayActor Actor)
    : IRequest<AppointmentResponse>;
