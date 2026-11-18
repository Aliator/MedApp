using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Appointments.Responses;
using MediatR;

namespace MedApp.Application.Appointments.Commands.RecordAppointmentOutcome;

/// <summary>
/// Marks a Booked appointment Attended or DidNotAttend (<paramref name="Outcome"/>) once it has started, or corrects
/// one already marked.
/// </summary>
public sealed record RecordAppointmentOutcomeCommand(Guid AppointmentId, string Outcome, PathwayActor Actor)
    : IRequest<AppointmentResponse>;
