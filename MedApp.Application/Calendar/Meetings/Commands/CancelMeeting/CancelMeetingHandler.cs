using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Teams;
using MedApp.Application.Mdt.Common;
using MedApp.Application.Mdt.Repositories;
using MedApp.Application.Notifications.Common;
using MediatR;

namespace MedApp.Application.Calendar.Meetings.Commands.CancelMeeting;

public sealed class CancelMeetingHandler(
    IMeetingRepository repository,
    INotificationSender notificationSender,
    IMdtItemRepository mdtItems,
    ICalendarRepository calendarRepository,
    ITeamBoundary boundary,
    TimeProvider timeProvider)
    : IRequestHandler<CancelMeetingCommand>
{
    public async Task Handle(CancelMeetingCommand request, CancellationToken cancellationToken)
    {
        var meeting = await repository.GetForUpdateAsync(request.MeetingId, cancellationToken)
                      ?? throw new NotFoundException(MeetingWalls.NotFoundMessage(request.MeetingId));
        // Another team's meeting is a 404, as if it weren't there (MC11).
        await MeetingWalls.EnsureVisibleAsync(meeting, request.Actor.Teams, calendarRepository, boundary, cancellationToken);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        CalendarGuards.EnsureNotPast(UkTime.DateOf(meeting.StartUtc), UkTime.DateOf(now));
        // An MDT with outcomes recorded stays; one without has its items' meeting cleared in the same save, keeping them listed.
        await MdtMeetings.EnsureCanCancelAsync(meeting, mdtItems, cancellationToken);

        await repository.DeleteAsync(meeting, cancellationToken);

        foreach (var notification in MeetingNotifications.AfterCancelling(meeting, request.Actor, now))
            await notificationSender.SendAsync(notification, cancellationToken);
    }
}
