using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Notifications.Common;
using MedApp.Contracts.Calendar.Responses;
using MedApp.Domain.Calendar;
using MediatR;

namespace MedApp.Application.Calendar.Meetings.Commands.UpdateMeeting;

public sealed class UpdateMeetingHandler(
    IMeetingRepository repository,
    ICalendarRepository calendarRepository,
    ICalendarLock calendarLock,
    IIdentityReadService identityReadService,
    INotificationSender notificationSender,
    TimeProvider timeProvider)
    : IRequestHandler<UpdateMeetingCommand, MeetingResponse>
{
    public async Task<MeetingResponse> Handle(UpdateMeetingCommand request, CancellationToken cancellationToken)
    {
        var existing = await repository.GetAsync(request.MeetingId, cancellationToken)
                       ?? throw new NotFoundException($"Meeting '{request.MeetingId}' was not found.");

        var start = UkTime.Utc(request.StartUtc);
        var end = UkTime.Utc(request.EndUtc);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var today = UkTime.DateOf(now);
        CalendarGuards.EnsureNotPast(UkTime.DateOf(existing.StartUtc), today);
        CalendarGuards.EnsureNotPast(UkTime.DateOf(start), today);

        var attendees = await identityReadService.GetStaffOrThrowAsync(request.AttendeeUserIds, cancellationToken);
        var previousAttendees = existing.Attendees.Select(a => a.StaffUserId).ToList();
        var locked = previousAttendees.Concat(attendees.Select(a => a.UserId)).Distinct().ToList();

        // Holding the locks of everyone going before and after, so nothing can take the new time meanwhile.
        var (meeting, before) = await calendarLock.RunAsync(locked, async ct =>
        {
            var tracked = await repository.GetForUpdateAsync(request.MeetingId, ct)
                          ?? throw new NotFoundException($"Meeting '{request.MeetingId}' was not found.");

            // Someone else changed who's going since it was read: their locks weren't taken.
            if (tracked.Attendees.Any(a => !locked.Contains(a.StaffUserId)))
                throw new ConflictException(MeetingLimits.ChangedMeanwhile);

            await calendarRepository.EnsureEveryoneFreeAsync(attendees, start, end, tracked.Id, ct);

            var was = (Attendees: tracked.Attendees.Select(a => a.StaffUserId).ToList(), tracked.StartUtc, tracked.EndUtc);
            var wanted = attendees.Select(a => a.UserId).ToHashSet();

            tracked.Title = request.Title.Trim();
            tracked.Notes = request.Notes?.Trim() ?? string.Empty;
            tracked.StartUtc = start;
            tracked.EndUtc = end;
            tracked.LastUpdated = now;
            tracked.Attendees.RemoveAll(a => !wanted.Contains(a.StaffUserId));
            tracked.Attendees.AddRange(MeetingStaff.Attendees(
                tracked.Id, wanted.Where(id => tracked.Attendees.All(a => a.StaffUserId != id))));

            await repository.SaveAsync(ct);
            return (tracked, was);
        }, cancellationToken);

        foreach (var notification in MeetingNotifications.AfterChanging(
                     meeting, before.Attendees, before.StartUtc, before.EndUtc, request.Actor, now))
            await notificationSender.SendAsync(notification, cancellationToken);

        var usernames = await identityReadService.GetUsernamesByIdAsync(MeetingMapper.UserIds(meeting), cancellationToken);
        return MeetingMapper.ToResponse(meeting, usernames, request.Actor.IsAdmin, today);
    }
}
