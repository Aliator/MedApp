using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Appointments.Responses;
using MediatR;

namespace MedApp.Application.Appointments.Commands.UpdateAppointmentNotes;

/// <summary>Replaces an appointment's notes (not once it's cancelled).</summary>
public sealed record UpdateAppointmentNotesCommand(Guid AppointmentId, string Notes, PathwayActor Actor)
    : IRequest<AppointmentResponse>;
