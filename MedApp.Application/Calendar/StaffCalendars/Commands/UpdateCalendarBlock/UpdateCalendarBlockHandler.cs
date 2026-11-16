using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Contracts.Calendar.Responses;
using MediatR;

namespace MedApp.Application.Calendar.StaffCalendars.Commands.UpdateCalendarBlock;

public sealed class UpdateCalendarBlockHandler(
    ICalendarRepository repository,
    ICalendarSetupRepository setupRepository,
    ICalendarLock calendarLock,
    TimeProvider timeProvider)
    : IRequestHandler<UpdateCalendarBlockCommand, CalendarBlockResponse>
{
    public async Task<CalendarBlockResponse> Handle(UpdateCalendarBlockCommand request, CancellationToken cancellationToken)
    {
        var existing = await repository.GetBlockAsync(request.BlockId, cancellationToken)
                       ?? throw new NotFoundException($"Block '{request.BlockId}' was not found.");
        request.Actor.EnsureCanChange(existing.StaffUserId);

        var start = UkTime.Utc(request.StartUtc);
        var end = UkTime.Utc(request.EndUtc);
        var date = UkTime.DateOf(start);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var today = UkTime.DateOf(now);
        CalendarGuards.EnsureNotPast(UkTime.DateOf(existing.StartUtc), today);
        CalendarGuards.EnsureNotPast(date, today);

        var activityType = await setupRepository.GetActivityTypeForBlockAsync(
            request.ActivityTypeId, existing.ActivityTypeId, cancellationToken);

        var block = await calendarLock.RunAsync([existing.StaffUserId], async ct =>
        {
            var tracked = await repository.GetBlockForUpdateAsync(request.BlockId, ct)
                          ?? throw new NotFoundException($"Block '{request.BlockId}' was not found.");

            var data = await repository.GetCalendarDataAsync(tracked.StaffUserId, date, date, ct);
            if (CalendarClashes.Find(data, start, end, ignoreBlockId: tracked.Id) is { } clash)
                throw new ConflictException(clash);

            tracked.ActivityTypeId = activityType.Id;
            tracked.StartUtc = start;
            tracked.EndUtc = end;
            tracked.Note = request.Note?.Trim() ?? string.Empty;
            tracked.LastUpdated = now;
            await repository.SaveAsync(ct);
            return tracked;
        }, cancellationToken);

        return CalendarMapper.ToResponse(block, canEditCalendar: true, today, overlapsAppointment: false, activityType);
    }
}
