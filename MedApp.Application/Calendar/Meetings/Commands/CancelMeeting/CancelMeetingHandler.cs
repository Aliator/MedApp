using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Notifications.Common;
using MediatR;

namespace MedApp.Application.Calendar.Meetings.Commands.CancelMeeting;

public sealed class CancelMeetingHandler(
    IMeetingRepository repository,
    INotificationSender notificationSender,
    TimeProvider timeProvider)
    : IRequestHandler<CancelMeetingCommand>
{
    public async Task Handle(CancelMeetingCommand request, CancellationToken cancellationToken)
    {
        var meeting = await repository.GetForUpdateAsync(request.MeetingId, cancellationToken)
                      ?? throw new NotFoundException($"Meeting '{request.MeetingId}' was not found.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        CalendarGuards.EnsureNotPast(UkTime.DateOf(meeting.StartUtc), UkTime.DateOf(now));

        await repository.DeleteAsync(meeting, cancellationToken);

        foreach (var notification in MeetingNotifications.AfterCancelling(meeting, request.Actor, now))
            await notificationSender.SendAsync(notification, cancellationToken);
    }
}
