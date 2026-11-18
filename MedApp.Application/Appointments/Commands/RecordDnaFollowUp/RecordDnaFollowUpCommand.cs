using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Appointments.Responses;
using MediatR;

namespace MedApp.Application.Appointments.Commands.RecordDnaFollowUp;

/// <summary>
/// Records what booking staff decided after a Did not attend (centre plan decision 132): Rebook, ClinicianToReview or
/// NoFurtherAppointment, with an optional detail (required for NoFurtherAppointment: who decided it). It changes nothing
/// else; MedApp never discharges anyone or changes a pathway because of a missed appointment.
/// </summary>
public sealed record RecordDnaFollowUpCommand(Guid AppointmentId, string Decision, string? Detail, PathwayActor Actor)
    : IRequest<AppointmentResponse>;
