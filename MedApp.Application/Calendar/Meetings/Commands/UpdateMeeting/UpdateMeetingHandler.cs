using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Mdt.Common;
using MedApp.Application.Mdt.Repositories;
using MedApp.Application.Specialties.Repositories;
using MedApp.Application.Notifications.Common;
using MedApp.Contracts.Calendar.Responses;
using MedApp.Domain.Calendar;
using MediatR;

namespace MedApp.Application.Calendar.Meetings.Commands.UpdateMeeting;

/// <summary>
/// Changes a meeting, holding the calendar locks of everyone going before and after. It's refused (a 409) when the
/// meeting has changed since the caller read it (<see cref="UpdateMeetingCommand.ExpectedLastUpdated"/>), so a change
/// made from a stale screen or a drag can't silently undo another admin's. Only the attendees it newly adds must be
/// staff; anyone already going whose account has gone (or who is no longer staff) is dropped, untold
/// (<see cref="MeetingStaff.ChooseOrThrow"/>). Those who declined it never stop a change (they aren't going). Moving it
/// (its start or end) puts every reply back to no reply, except the decline of anyone busy at the new time
/// (<see cref="MeetingReplies.ResetAfterMove"/>), and those still going are asked to reply again; any other change keeps
/// replies, and those added start with none.
/// </summary>
public sealed class UpdateMeetingHandler(
    IMeetingRepository repository,
    ICalendarRepository calendarRepository,
    ICalendarLock calendarLock,
    IIdentityReadService identityReadService,
    INotificationSender notificationSender,
    ISpecialtyRepository specialties,
    IMdtItemRepository mdtItems,
    TimeProvider timeProvider)
    : IRequestHandler<UpdateMeetingCommand, MeetingResponse>
{
    public async Task<MeetingResponse> Handle(UpdateMeetingCommand request, CancellationToken cancellationToken)
    {
        var existing = await repository.GetAsync(request.MeetingId, cancellationToken)
                       ?? throw new NotFoundException($"Meeting '{request.MeetingId}' was not found.");

        // The validator requires it; compared here first so a stale change fails without taking anyone's lock.
        var expected = UkTime.Utc(request.ExpectedLastUpdated!.Value);
        EnsureUnchanged(existing, expected);

        var start = UkTime.Utc(request.StartUtc);
        var end = UkTime.Utc(request.EndUtc);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var today = UkTime.DateOf(now);
        CalendarGuards.EnsureNotPast(UkTime.DateOf(existing.StartUtc), today);
        CalendarGuards.EnsureNotPast(UkTime.DateOf(start), today);

        // An MDT keeps its specialty while items are on it.
        var mdtSpecialtyId = await MdtMeetings.ChooseSpecialtyAsync(
            request, existing.IsMdt ? existing.SpecialtyId : null, specialties, cancellationToken);
        await MdtMeetings.EnsureCanChangeAsync(existing, mdtSpecialtyId, mdtItems, cancellationToken);

        var previousAttendees = existing.Attendees.Select(a => a.StaffUserId).ToList();
        var staff = await identityReadService.GetStaffAsync(cancellationToken);
        var attendees = MeetingStaff.ChooseOrThrow(staff, request.AttendeeUserIds, previousAttendees);
        var locked = previousAttendees.Concat(attendees.Select(a => a.UserId)).Distinct().ToList();

        // Holding the locks of everyone going before and after, so nothing can take the new time meanwhile.
        var (meeting, before) = await calendarLock.RunAsync(locked, async ct =>
        {
            var tracked = await repository.GetForUpdateAsync(request.MeetingId, ct)
                          ?? throw new NotFoundException($"Meeting '{request.MeetingId}' was not found.");

            // Compared again with the copy read under the locks: someone may have saved a change since the first read.
            EnsureUnchanged(tracked, expected);

            // Someone else changed who's going since it was read: their locks weren't taken.
            if (tracked.Attendees.Any(a => !locked.Contains(a.StaffUserId)))
                throw new ConflictException(MeetingLimits.ChangedMeanwhile);

            // Those who declined aren't going, so the time isn't theirs to keep free: they're never checked, kept where
            // it is or moved. A move asks everyone else again; of those who declined, it asks again only those free at the
            // new time, and the rest keep their decline (they still aren't going, and asking would clash with what they
            // have on).
            var moved = start != tracked.StartUtc || end != tracked.EndUtc;
            var decliners = MeetingReplies.Decliners(tracked);
            await calendarRepository.EnsureEveryoneFreeAsync(
                attendees.Where(a => !decliners.Contains(a.UserId)).ToList(), start, end, tracked.Id, ct);
            var keepDeclined = moved
                ? await BusyAtAsync(attendees.Where(a => decliners.Contains(a.UserId)), start, end, tracked.Id, ct)
                : null;

            var was = (Attendees: tracked.Attendees.Select(a => a.StaffUserId).ToList(), tracked.StartUtc, tracked.EndUtc);
            var wanted = attendees.Select(a => a.UserId).ToHashSet();

            tracked.Title = request.Title.Trim();
            tracked.Notes = request.Notes?.Trim() ?? string.Empty;
            tracked.IsMdt = mdtSpecialtyId is not null;
            tracked.SpecialtyId = mdtSpecialtyId;
            tracked.StartUtc = start;
            tracked.EndUtc = end;
            tracked.LastUpdated = now;
            if (moved)
                MeetingReplies.ResetAfterMove(tracked, keepDeclined);
            tracked.Attendees.RemoveAll(a => !wanted.Contains(a.StaffUserId));
            tracked.Attendees.AddRange(MeetingStaff.Attendees(
                tracked.Id, wanted.Where(id => tracked.Attendees.All(a => a.StaffUserId != id))));

            await repository.SaveAsync(ct);
            return (tracked, was);
        }, cancellationToken);

        // Only staff are told: nobody whose account has gone (or who is no longer staff) hears they were taken off.
        var staffIds = staff.Select(s => s.UserId).ToHashSet();
        foreach (var notification in MeetingNotifications.AfterChanging(
                     meeting, before.Attendees.Where(staffIds.Contains).ToList(), before.StartUtc, before.EndUtc, request.Actor, now))
            await notificationSender.SendAsync(notification, cancellationToken);

        var usernames = await identityReadService.GetUsernamesByIdAsync(MeetingMapper.UserIds(meeting), cancellationToken);
        return MeetingMapper.ToResponse(meeting, usernames, request.Actor, now);
    }

    /// <summary>Of these people, those with something on at the time (the meeting itself left out). Run holding their locks.</summary>
    private async Task<IReadOnlySet<Guid>> BusyAtAsync(
        IEnumerable<(Guid UserId, string Username)> people, DateTime startUtc, DateTime endUtc, Guid meetingId, CancellationToken ct)
    {
        var ids = people.Select(p => p.UserId).ToList();
        if (ids.Count == 0)
            return new HashSet<Guid>();

        var date = UkTime.DateOf(startUtc);
        return (await calendarRepository.GetCalendarsAsync(ids, date, date, ct))
            .Where(c => CalendarClashes.FindFirst(c, startUtc, endUtc, ignoreMeetingId: meetingId) is not null)
            .Select(c => c.StaffUserId)
            .ToHashSet();
    }

    /// <summary>A 409 when the meeting isn't as the caller last read it.</summary>
    private static void EnsureUnchanged(Meeting meeting, DateTime expectedLastUpdated)
    {
        if (meeting.LastUpdated != expectedLastUpdated)
            throw new ConflictException(MeetingLimits.ChangedMeanwhile);
    }
}
