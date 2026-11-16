using MedApp.Application.Activity.Common;
using MedApp.Application.Appointments.Common;
using MedApp.Application.Appointments.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Contracts.Appointments.Responses;
using MedApp.Domain.Activity;
using MediatR;

namespace MedApp.Application.Appointments.Queries.GetAppointmentById;

/// <summary>One appointment. Opening it is recorded as viewing its patient (once per person every few minutes).</summary>
public sealed class GetAppointmentByIdHandler(
    IAppointmentRepository repository,
    IIdentityReadService identityReadService,
    IActivityLog activityLog,
    TimeProvider timeProvider)
    : IRequestHandler<GetAppointmentByIdQuery, AppointmentResponse>
{
    public async Task<AppointmentResponse> Handle(GetAppointmentByIdQuery request, CancellationToken cancellationToken)
    {
        var appointment = await repository.GetByIdAsync(request.AppointmentId, cancellationToken)
                          ?? throw new NotFoundException($"Appointment '{request.AppointmentId}' was not found.");

        await activityLog.RecordViewsAsync(ActivityAction.PatientViewed, [appointment.PatientId], cancellationToken);

        return await AppointmentMapper.ToResponseAsync(
            appointment, identityReadService, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
    }
}
