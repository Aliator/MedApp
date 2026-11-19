using MedApp.Application.Activity.Common;
using MedApp.Application.Appointments.Common;
using MedApp.Application.Appointments.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Teams;
using MedApp.Contracts.Appointments.Responses;
using MedApp.Domain.Activity;
using MediatR;

namespace MedApp.Application.Appointments.Queries.GetAppointmentById;

/// <summary>
/// One appointment. Behind the viewer's team wall it's a 404, as if it weren't there, recorded. Opening it is recorded as
/// viewing its patient (once per person every few minutes), with why they could (<c>access</c>).
/// </summary>
public sealed class GetAppointmentByIdHandler(
    IAppointmentRepository repository,
    IIdentityReadService identityReadService,
    IActivityLog activityLog,
    ITeamBoundary boundary,
    TimeProvider timeProvider)
    : IRequestHandler<GetAppointmentByIdQuery, AppointmentResponse>
{
    public async Task<AppointmentResponse> Handle(GetAppointmentByIdQuery request, CancellationToken cancellationToken)
    {
        var appointment = await repository.GetByIdAsync(request.AppointmentId, cancellationToken)
                          ?? throw new NotFoundException(AppointmentWalls.NotFoundMessage(request.AppointmentId));
        var access = await boundary.EnsureVisibleAsync(appointment, request.Teams, cancellationToken);

        await activityLog.RecordViewsAsync(
            ActivityAction.PatientViewed, [appointment.PatientId], cancellationToken, details: new { access = access.ToString() });

        return await AppointmentMapper.ToResponseAsync(
            appointment, identityReadService, timeProvider.GetUtcNow().UtcDateTime, cancellationToken, request.Teams);
    }
}
