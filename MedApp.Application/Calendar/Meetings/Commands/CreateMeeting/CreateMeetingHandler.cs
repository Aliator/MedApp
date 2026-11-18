using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Identity;
using MedApp.Application.Mdt.Common;
using MedApp.Application.Specialties.Repositories;
using MedApp.Application.Notifications.Common;
using MedApp.Contracts.Calendar.Responses;
using MedApp.Domain.Calendar;
using MediatR;

namespace MedApp.Application.Calendar.Meetings.Commands.CreateMeeting;

public sealed class CreateMeetingHandler(
    IMeetingRepository repository,
    ICalendarRepository calendarRepository,
    ICalendarLock calendarLock,
    IIdentityReadService identityReadService,
    INotificationSender notificationSender,
    ISpecialtyRepository specialties,
    TimeProvider timeProvider)
    : IRequestHandler<CreateMeetingCommand, MeetingResponse>
{
    public async Task<MeetingResponse> Handle(CreateMeetingCommand request, CancellationToken cancellationToken)
    {
        var start = UkTime.Utc(request.StartUtc);
        var end = UkTime.Utc(request.EndUtc);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var today = UkTime.DateOf(now);
        CalendarGuards.EnsureNotPast(UkTime.DateOf(start), today);

        var attendees = await identityReadService.GetStaffOrThrowAsync(request.AttendeeUserIds, cancellationToken);
        var mdtSpecialtyId = await MdtMeetings.ChooseSpecialtyAsync(request, null, specialties, cancellationToken);

        // Checked and saved holding every attendee's calendar lock, so nothing can take the time meanwhile.
        var meeting = await calendarLock.RunAsync(attendees.Select(a => a.UserId).ToList(), async ct =>
        {
            await calendarRepository.EnsureEveryoneFreeAsync(attendees, start, end, null, ct);

            var id = Guid.NewGuid();
            var created = new Meeting
            {
                Id = id,
                Title = request.Title.Trim(),
                Notes = request.Notes?.Trim() ?? string.Empty,
                StartUtc = start,
                EndUtc = end,
                OrganiserUserId = request.Actor.UserId,
                IsMdt = mdtSpecialtyId is not null,
                SpecialtyId = mdtSpecialtyId,
                CreatedAtUtc = now,
                LastUpdated = now,
                Attendees = MeetingStaff.Attendees(id, attendees.Select(a => a.UserId))
            };
            await repository.AddAsync(created, ct);
            return created;
        }, cancellationToken);

        foreach (var notification in MeetingNotifications.AfterCreating(meeting, request.Actor, now))
            await notificationSender.SendAsync(notification, cancellationToken);

        var usernames = await identityReadService.GetUsernamesByIdAsync(MeetingMapper.UserIds(meeting), cancellationToken);
        return MeetingMapper.ToResponse(meeting, usernames, request.Actor, now);
    }
}
