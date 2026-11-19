using MedApp.Application.Appointments.Common;
using MedApp.Application.Appointments.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Teams;
using MedApp.Application.Notifications.Common;
using MedApp.Application.PatientMessages.Common;
using MedApp.Contracts.Appointments.Responses;
using MedApp.Domain.Appointments;
using MedApp.Domain.PatientMessages;
using MediatR;

namespace MedApp.Application.Appointments.Commands.CancelAppointment;

public sealed class CancelAppointmentHandler(
    IAppointmentRepository repository,
    IIdentityReadService identityReadService,
    INotificationSender notificationSender,
    IPatientMessageOutbox patientMessages,
    ITeamBoundary boundary,
    TimeProvider timeProvider)
    : IRequestHandler<CancelAppointmentCommand, AppointmentResponse>
{
    public async Task<AppointmentResponse> Handle(CancelAppointmentCommand request, CancellationToken cancellationToken)
    {
        // Cancelling stays allowed while the patient's record is restricted, so care can be paused safely.
        var appointment = await repository.GetForCancelOrThrowAsync(request.AppointmentId, boundary, request.Actor.Teams, cancellationToken);

        if (!AppointmentWorkflow.CanCancel(appointment))
            throw new ConflictException(
                $"An appointment that's {AppointmentWorkflow.Describe(appointment.Status)} can't be cancelled.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var previousStatus = appointment.Status;
        var staffUsername = await identityReadService.GetStaffUsernameAsync(appointment, cancellationToken);

        var change = new AppointmentChange(appointment, request.Actor, now, staffUsername);
        change.Cancel(request.Reason.Trim());

        // Telling the patient, saved with the cancellation (the job sends it): only for a booking still to come.
        if (request.TellPatient && previousStatus == AppointmentStatus.Booked && appointment.StartUtc > now)
            await patientMessages.StageAsync(
                appointment, PatientMessageKind.AppointmentCancelled, request.Actor.UserId, now, cancellationToken);

        await repository.SaveAsync(change.Events, cancellationToken);

        foreach (var notification in AppointmentNotifications.AfterCancelling(
                     appointment, previousStatus, request.Actor, now))
            await notificationSender.SendAsync(notification, cancellationToken);

        return await AppointmentMapper.ToResponseAsync(appointment, identityReadService, now, cancellationToken, request.Actor.Teams);
    }
}
