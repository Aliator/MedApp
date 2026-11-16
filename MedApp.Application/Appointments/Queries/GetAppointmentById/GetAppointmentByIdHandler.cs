using MedApp.Application.Appointments.Common;
using MedApp.Application.Appointments.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Contracts.Appointments.Responses;
using MediatR;

namespace MedApp.Application.Appointments.Queries.GetAppointmentById;

public sealed class GetAppointmentByIdHandler(
    IAppointmentRepository repository,
    IIdentityReadService identityReadService,
    TimeProvider timeProvider)
    : IRequestHandler<GetAppointmentByIdQuery, AppointmentResponse>
{
    public async Task<AppointmentResponse> Handle(GetAppointmentByIdQuery request, CancellationToken cancellationToken)
    {
        var appointment = await repository.GetByIdAsync(request.AppointmentId, cancellationToken)
                          ?? throw new NotFoundException($"Appointment '{request.AppointmentId}' was not found.");

        return await AppointmentMapper.ToResponseAsync(
            appointment, identityReadService, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
    }
}
