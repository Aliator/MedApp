using MedApp.Application.Appointments.Common;
using MedApp.Application.Appointments.Repositories;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Notifications.Common;
using MedApp.Contracts.Appointments.Responses;
using MedApp.Domain.Appointments;
using MedApp.Domain.Notifications;
using MediatR;

namespace MedApp.Application.Appointments.Commands.BookAppointment;

public sealed class BookAppointmentHandler(
    IAppointmentRepository repository,
    ICalendarRepository calendarRepository,
    ICalendarLock calendarLock,
    IIdentityReadService identityReadService,
    INotificationSender notificationSender,
    TimeProvider timeProvider)
    : IRequestHandler<BookAppointmentCommand, AppointmentResponse>
{
    public async Task<AppointmentResponse> Handle(BookAppointmentCommand request, CancellationToken cancellationToken)
    {
        var staffUsername = await identityReadService.GetUsernameOrThrowAsync(request.StaffUserId, cancellationToken);
        var start = UkTime.Utc(request.StartUtc);
        var end = UkTime.Utc(request.EndUtc);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        // Loaded, checked and saved holding the new member of staff's calendar lock, so two bookings can't race for a
        // slot. Moving it off someone's calendar can't make a clash there.
        var result = await calendarLock.RunAsync([request.StaffUserId], async ct =>
        {
            var appointment = await repository.GetForUpdateOrThrowAsync(request.AppointmentId, ct);

            if (!AppointmentWorkflow.CanBook(appointment) && !AppointmentWorkflow.CanReschedule(appointment))
                throw new ConflictException(
                    $"An appointment that's {AppointmentWorkflow.Describe(appointment.Status)} can't be booked or moved.");

            var before = new Before(appointment.Status, appointment.StaffUserId, appointment.StartUtc);

            var date = UkTime.DateOf(start);
            var data = await calendarRepository.GetCalendarDataAsync(request.StaffUserId, date, date, ct);
            if (CalendarClashes.Find(data, start, end, ignoreAppointmentId: appointment.Id) is { } clash)
                throw new ConflictException(clash);

            var previousStaffUsername = await identityReadService.GetStaffUsernameAsync(appointment, ct);
            var change = new AppointmentChange(appointment, request.Actor, now, previousStaffUsername);
            change.Book(request.StaffUserId, staffUsername, start, end);

            if (change.HasChanges)
                await repository.SaveAsync(change.Events, ct);

            return (Appointment: appointment, Before: before);
        }, cancellationToken);

        foreach (var notification in Notifications(result.Appointment, result.Before, request, now))
            await notificationSender.SendAsync(notification, cancellationToken);

        return await AppointmentMapper.ToResponseAsync(result.Appointment, identityReadService, now, cancellationToken);
    }

    private static IReadOnlyList<Notification> Notifications(
        Appointment appointment, Before before, BookAppointmentCommand request, DateTime nowUtc) =>
        AppointmentNotifications.AfterBooking(
            appointment,
            before.Status,
            before.StaffUserId,
            before.StartUtc,
            request.Actor,
            nowUtc);

    /// <summary>Where the appointment was before it was booked or moved.</summary>
    private sealed record Before(AppointmentStatus Status, Guid? StaffUserId, DateTime? StartUtc);
}
