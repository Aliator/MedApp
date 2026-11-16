using MedApp.Application.Procedures.Common;
using MedApp.Contracts.Appointments.Responses;
using MediatR;

namespace MedApp.Application.Appointments.Commands.UpdateAppointmentNotes;

/// <summary>Replaces an appointment's notes (not once it's cancelled).</summary>
public sealed record UpdateAppointmentNotesCommand(Guid AppointmentId, string Notes, ProcedureActor Actor)
    : IRequest<AppointmentResponse>;
