using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Appointments.Responses;
using MediatR;

namespace MedApp.Application.Appointments.Commands.CancelAppointment;

/// <summary>Cancels a Requested or Booked appointment, with a reason. Cancelled is final.</summary>
public sealed record CancelAppointmentCommand(Guid AppointmentId, string Reason, PathwayActor Actor)
    : IRequest<AppointmentResponse>;
