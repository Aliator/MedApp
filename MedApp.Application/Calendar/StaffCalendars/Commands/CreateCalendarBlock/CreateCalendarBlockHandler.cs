using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Contracts.Calendar.Responses;
using MedApp.Domain.Calendar;
using MediatR;

namespace MedApp.Application.Calendar.StaffCalendars.Commands.CreateCalendarBlock;

public sealed class CreateCalendarBlockHandler(
    ICalendarRepository repository,
    ICalendarSetupRepository setupRepository,
    ICalendarLock calendarLock,
    IIdentityReadService identityReadService,
    TimeProvider timeProvider)
    : IRequestHandler<CreateCalendarBlockCommand, CalendarBlockResponse>
{
    public async Task<CalendarBlockResponse> Handle(CreateCalendarBlockCommand request, CancellationToken cancellationToken)
    {
        request.Actor.EnsureCanChange(request.StaffUserId);
        await identityReadService.GetUsernameOrThrowAsync(request.StaffUserId, cancellationToken);

        var start = UkTime.Utc(request.StartUtc);
        var end = UkTime.Utc(request.EndUtc);
        var date = UkTime.DateOf(start);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var today = UkTime.DateOf(now);
        CalendarGuards.EnsureNotPast(date, today);

        var activityType = await setupRepository.GetActivityTypeForBlockAsync(request.ActivityTypeId, null, cancellationToken);

        // Checked and saved holding the member of staff's calendar lock, so nothing can take the slot meanwhile.
        var block = await calendarLock.RunAsync([request.StaffUserId], async ct =>
        {
            var data = await repository.GetCalendarDataAsync(request.StaffUserId, date, date, ct);
            if (CalendarClashes.Find(data, start, end) is { } clash)
                throw new ConflictException(clash);

            var created = new CalendarBlock
            {
                Id = Guid.NewGuid(),
                StaffUserId = request.StaffUserId,
                ActivityTypeId = activityType.Id,
                StartUtc = start,
                EndUtc = end,
                Note = request.Note?.Trim() ?? string.Empty,
                CreatedAtUtc = now,
                CreatedByUserId = request.Actor.UserId,
                LastUpdated = now
            };
            await repository.AddBlockAsync(created, ct);
            return created;
        }, cancellationToken);

        return CalendarMapper.ToResponse(block, canEditCalendar: true, today, overlapsAppointment: false, activityType);
    }
}
