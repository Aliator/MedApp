using MedApp.Application.Common.Teams;
using MedApp.Contracts.PatientMessages.Responses;
using MediatR;

namespace MedApp.Application.PatientMessages.Queries.GetAppointmentMessages;

/// <summary>
/// The texts and emails about an appointment, newest first. They follow the appointment's team wall
/// (<paramref name="Scope"/>; null only outside a request, which is unscoped).
/// </summary>
public sealed record GetAppointmentMessagesQuery(Guid AppointmentId, TeamScope? Scope = null) : IRequest<List<PatientMessageResponse>>
{
    /// <summary>The viewer's team walls: the request's, or the system's outside a request.</summary>
    public TeamScope Teams => Scope ?? TeamScope.System;
}
