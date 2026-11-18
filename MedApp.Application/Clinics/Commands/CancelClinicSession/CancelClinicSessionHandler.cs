using MedApp.Application.Activity.Common;
using MedApp.Application.Appointments.Common;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Clinics.Common;
using MedApp.Application.Clinics.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Notifications.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Clinics.Responses;
using MedApp.Domain.Activity;
using MedApp.Domain.Appointments;
using MedApp.Domain.Clinics;
using MediatR;

namespace MedApp.Application.Clinics.Commands.CancelClinicSession;

/// <summary>
/// Cancels a session and its Booked appointments, each through <see cref="AppointmentChange"/> with the reason, so its
/// history records it; Attended and Did not attend ones stay as they are. A restricted patient's appointment is
/// cancelled too, as cancelling pauses care safely. The patients to tell are listed, which is a read of their records
/// (<see cref="ActivityAction.PatientListed"/>). The clinician is told when someone else cancels it, once for the session
/// (<see cref="ClinicNotifications"/>), not once for each appointment. Stage visits follow the rule for any cancelled
/// appointment: nothing is asked for again.
/// </summary>
public sealed class CancelClinicSessionHandler(
    IClinicRepository repository,
    ICalendarLock calendarLock,
    IIdentityReadService identityReadService,
    INotificationSender notificationSender,
    IActivityLog activityLog,
    TimeProvider timeProvider)
    : IRequestHandler<CancelClinicSessionCommand, CancelClinicSessionResponse>
{
    public async Task<CancelClinicSessionResponse> Handle(CancelClinicSessionCommand request, CancellationToken cancellationToken)
    {
        var existing = await repository.GetSessionAsync(request.SessionId, cancellationToken)
                       ?? throw new NotFoundException($"Clinic session '{request.SessionId}' was not found.");

        var expected = UkTime.Utc(request.ExpectedLastUpdated!.Value);
        ClinicRules.EnsureUnchanged(existing, expected);
        ClinicRules.EnsurePlanned(existing);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        CalendarGuards.EnsureNotPast(existing.Date, UkTime.DateOf(now));

        var reason = request.Reason.Trim();
        var usernames = await identityReadService.GetUsernamesByIdAsync([existing.StaffUserId], cancellationToken);
        var staffUsername = usernames.GetValueOrDefault(existing.StaffUserId) ?? string.Empty;
        var actor = new PathwayActor(request.Actor.UserId, request.Actor.Username, request.Actor.Permissions);

        // Holding the clinician's lock (bookings into it take it) and the room's.
        var (session, cancelled, kept) = await calendarLock.RunAsync(
            SessionPlacement.Locks(existing.StaffUserId, existing.RoomId), async ct =>
            {
                var tracked = await repository.GetSessionForUpdateAsync(request.SessionId, ct)
                              ?? throw new NotFoundException($"Clinic session '{request.SessionId}' was not found.");
                ClinicRules.EnsureUnchanged(tracked, expected);
                ClinicRules.EnsurePlanned(tracked);

                var appointments = await repository.GetSessionAppointmentsForUpdateAsync(tracked.Id, ct);
                var events = new List<AppointmentEvent>();
                var cancelledNow = new List<Appointment>();
                foreach (var appointment in appointments.Where(AppointmentWorkflow.CanCancel))
                {
                    var change = new AppointmentChange(appointment, actor, now, staffUsername);
                    change.Cancel(reason);
                    events.AddRange(change.Events);
                    cancelledNow.Add(appointment);
                }

                tracked.Status = ClinicSessionStatus.Cancelled;
                tracked.CancelledAtUtc = now;
                tracked.CancelledByUserId = request.Actor.UserId;
                tracked.CancelReason = reason;
                tracked.LastUpdated = now;

                await repository.SaveAsync(events, ClinicRules.ChangedMeanwhile, ct);
                IReadOnlyList<DateTime> stayed = appointments.Except(cancelledNow)
                    .Where(a => a.StartUtc is not null)
                    .Select(a => a.StartUtc!.Value)
                    .ToList();
                return (tracked, (IReadOnlyList<Appointment>)cancelledNow, stayed);
            }, cancellationToken);

        // The list of whom to tell names the patients: a read of their records, as any list of them is.
        await activityLog.RecordViewsAsync(ActivityAction.PatientListed, cancelled.Select(a => a.PatientId), cancellationToken);

        foreach (var notification in ClinicNotifications.AfterCancelling(session, request.Actor, now))
            await notificationSender.SendAsync(notification, cancellationToken);

        return new CancelClinicSessionResponse
        {
            Session = ClinicMapper.ToResponse(session, staffUsername, kept, now, request.Actor.CanManageClinics),
            PatientsToTell = cancelled.OrderBy(a => a.StartUtc).Select(ClinicMapper.ToPatientToTell).ToList()
        };
    }
}
