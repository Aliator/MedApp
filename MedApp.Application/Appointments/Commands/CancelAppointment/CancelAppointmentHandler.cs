using MedApp.Application.Appointments.Common;
using MedApp.Application.Appointments.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Notifications.Common;
using MedApp.Contracts.Appointments.Responses;
using MediatR;

namespace MedApp.Application.Appointments.Commands.CancelAppointment;

public sealed class CancelAppointmentHandler(
    IAppointmentRepository repository,
    IIdentityReadService identityReadService,
    INotificationSender notificationSender,
    TimeProvider timeProvider)
    : IRequestHandler<CancelAppointmentCommand, AppointmentResponse>
{
    public async Task<AppointmentResponse> Handle(CancelAppointmentCommand request, CancellationToken cancellationToken)
    {
        var appointment = await repository.GetForUpdateOrThrowAsync(request.AppointmentId, cancellationToken);

        if (!AppointmentWorkflow.CanCancel(appointment))
            throw new ConflictException(
                $"An appointment that's {AppointmentWorkflow.Describe(appointment.Status)} can't be cancelled.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var previousStatus = appointment.Status;
        var staffUsername = await identityReadService.GetStaffUsernameAsync(appointment, cancellationToken);

        var change = new AppointmentChange(appointment, request.Actor, now, staffUsername);
        change.Cancel(request.Reason.Trim());

        await repository.SaveAsync(change.Events, cancellationToken);

        foreach (var notification in AppointmentNotifications.AfterCancelling(
                     appointment, appointment.Procedure?.Title, previousStatus, request.Actor, now))
            await notificationSender.SendAsync(notification, cancellationToken);

        return await AppointmentMapper.ToResponseAsync(appointment, identityReadService, now, cancellationToken);
    }
}
