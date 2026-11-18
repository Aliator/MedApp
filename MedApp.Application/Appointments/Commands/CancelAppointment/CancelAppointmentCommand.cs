using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Appointments.Responses;
using MediatR;

namespace MedApp.Application.Appointments.Commands.CancelAppointment;

/// <summary>
/// Cancels a Requested or Booked appointment, with a reason. Cancelled is final. With <paramref name="TellPatient"/>, a
/// Booked one still to come texts or emails the patient (<c>AppointmentCancelled</c>), as they chose, when patient
/// messages are on; the message never gives the reason.
/// </summary>
public sealed record CancelAppointmentCommand(Guid AppointmentId, string Reason, PathwayActor Actor, bool TellPatient = false)
    : IRequest<AppointmentResponse>;
