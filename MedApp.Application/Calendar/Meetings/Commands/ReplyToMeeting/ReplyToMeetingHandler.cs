using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Notifications.Common;
using MedApp.Application.Procedures.Common;
using MedApp.Contracts.Calendar.Responses;
using MedApp.Domain.Calendar;
using MediatR;

namespace MedApp.Application.Calendar.Meetings.Commands.ReplyToMeeting;

/// <summary>
/// Records the signed-in member of staff's reply to a meeting (<see cref="MeetingReplies"/>), holding their calendar
/// lock: a change to the meeting holds it too (they're going), so a reply can't cross a move, and a booking into time
/// a decline frees waits for it. A cancelled meeting is deleted, so it's a 404; someone not going is a 403; one that
/// has ended is a 409. Accepting again after declining is a 409 if that time has been taken since (the decline freed
/// it). The same reply again changes nothing. A decline (not a repeat of one) tells the organiser, unless it's theirs
/// or their account has gone.
/// </summary>
public sealed class ReplyToMeetingHandler(
    IMeetingRepository repository,
    ICalendarRepository calendarRepository,
    ICalendarLock calendarLock,
    IIdentityReadService identityReadService,
    INotificationSender notificationSender,
    TimeProvider timeProvider)
    : IRequestHandler<ReplyToMeetingCommand, MeetingResponse>
{
    public async Task<MeetingResponse> Handle(ReplyToMeetingCommand request, CancellationToken cancellationToken)
    {
        var reply = ProcedureEnums.Parse<MeetingReply>(request.Reply);
        var actor = request.Actor;
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var (meeting, before) = await calendarLock.RunAsync([actor.UserId], async ct =>
        {
            var tracked = await repository.GetForUpdateAsync(request.MeetingId, ct)
                          ?? throw new NotFoundException($"Meeting '{request.MeetingId}' was not found.");

            var previous = MeetingReplies.Reply(tracked, actor.UserId, reply, now);
            if (previous == reply)
                return (tracked, previous);

            // The decline freed the time on their calendar, and something may have been put there since.
            if (previous == MeetingReply.Declined)
                await EnsureStillFreeAsync(tracked, actor.UserId, ct);

            await repository.SaveAsync(ct);
            return (tracked, previous);
        }, cancellationToken);

        var usernames = await identityReadService.GetUsernamesByIdAsync(MeetingMapper.UserIds(meeting), cancellationToken);

        if (reply == MeetingReply.Declined && before != MeetingReply.Declined
            && usernames.ContainsKey(meeting.OrganiserUserId)
            && MeetingNotifications.AfterDeclining(meeting, actor, now) is { } notification)
            await notificationSender.SendAsync(notification, cancellationToken);

        return MeetingMapper.ToResponse(meeting, usernames, actor, now);
    }

    /// <summary>A 409 naming what's now at the meeting's time on this person's calendar, if anything is.</summary>
    private async Task EnsureStillFreeAsync(Meeting meeting, Guid staffUserId, CancellationToken ct)
    {
        var date = UkTime.DateOf(meeting.StartUtc);
        var calendar = (await calendarRepository.GetCalendarsAsync([staffUserId], date, date, ct)).Single();
        if (CalendarClashes.Find(calendar, meeting.StartUtc, meeting.EndUtc, ignoreMeetingId: meeting.Id) is { } clash)
            throw new ConflictException($"{MeetingReplies.TakenSinceDeclining} {clash}");
    }
}
