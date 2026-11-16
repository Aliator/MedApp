using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Contracts.Calendar.Responses;
using MedApp.Domain.Calendar;
using MediatR;

namespace MedApp.Application.Calendar.StaffCalendars.Commands.ChangeTemplateBlockForDay;

public sealed class ChangeTemplateBlockForDayHandler(
    ICalendarRepository repository,
    ICalendarSetupRepository setupRepository,
    ICalendarLock calendarLock,
    IIdentityReadService identityReadService,
    TimeProvider timeProvider)
    : IRequestHandler<ChangeTemplateBlockForDayCommand, CalendarBlockResponse>
{
    public async Task<CalendarBlockResponse> Handle(ChangeTemplateBlockForDayCommand request, CancellationToken cancellationToken)
    {
        request.Actor.EnsureCanChange(request.StaffUserId);
        await identityReadService.GetUsernameOrThrowAsync(request.StaffUserId, cancellationToken);

        var start = UkTime.Utc(request.StartUtc);
        var end = UkTime.Utc(request.EndUtc);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var today = UkTime.DateOf(now);
        CalendarGuards.EnsureNotPast(request.Date, today);

        var result = await calendarLock.RunAsync([request.StaffUserId], async ct =>
        {
            var data = await repository.GetCalendarDataAsync(request.StaffUserId, request.Date, request.Date, ct);
            var templateBlock = TemplateBlockForDay.Find(data, request.TemplateBlockId, request.Date);

            var activityType = await setupRepository.GetActivityTypeForBlockAsync(
                request.ActivityTypeId, templateBlock.Source.ActivityTypeId, ct);

            if (CalendarClashes.Find(data, start, end, ignoreTemplateBlockId: templateBlock.Source.Id) is { } clash)
                throw new ConflictException(clash);

            var replacement = new CalendarBlock
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
            await repository.AddBlockExceptionAsync(
                TemplateBlockForDay.Exception(request.StaffUserId, request.Date, templateBlock), replacement, ct);
            return (Block: replacement, ActivityType: activityType);
        }, cancellationToken);

        return CalendarMapper.ToResponse(
            result.Block, canEditCalendar: true, today, overlapsAppointment: false, result.ActivityType);
    }
}
