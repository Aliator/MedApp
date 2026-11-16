using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Contracts.Calendar.Responses;
using MedApp.Domain.Calendar;
using MediatR;

namespace MedApp.Application.Calendar.StaffCalendars.Commands.ApplyDayTemplate;

public sealed class ApplyDayTemplateHandler(
    ICalendarRepository repository,
    ICalendarSetupRepository setupRepository,
    ICalendarLock calendarLock,
    IIdentityReadService identityReadService,
    TimeProvider timeProvider)
    : IRequestHandler<ApplyDayTemplateCommand, CalendarDayResponse>
{
    public async Task<CalendarDayResponse> Handle(ApplyDayTemplateCommand request, CancellationToken cancellationToken)
    {
        request.Actor.EnsureCanChange(request.StaffUserId);
        await identityReadService.GetUsernameOrThrowAsync(request.StaffUserId, cancellationToken);

        var today = UkTime.Today(timeProvider);
        CalendarGuards.EnsureNotPast(request.Date, today);

        if (request.DayTemplateId is { } dayTemplateId)
        {
            var template = await setupRepository.GetDayTemplateOrThrowAsync(dayTemplateId, today, cancellationToken);
            if (template.IsArchived)
                throw new ConflictException($"The '{template.Name}' day template is archived, so it can't be chosen.");
        }

        // Under the calendar lock, so two changes to the same day can't both add its override.
        await calendarLock.RunAsync([request.StaffUserId], async ct =>
        {
            var dayOverride = await repository.GetDayOverrideForUpdateAsync(request.StaffUserId, request.Date, ct);
            if (dayOverride is null)
            {
                await repository.AddDayOverrideAsync(new CalendarDayOverride
                {
                    Id = Guid.NewGuid(),
                    StaffUserId = request.StaffUserId,
                    Date = request.Date,
                    DayTemplateId = request.DayTemplateId
                }, ct);
            }
            else if (dayOverride.DayTemplateId != request.DayTemplateId)
            {
                dayOverride.DayTemplateId = request.DayTemplateId;
                await repository.SaveAsync(ct);
            }

            return true;
        }, cancellationToken);

        var data = await repository.GetCalendarDataAsync(request.StaffUserId, request.Date, request.Date, cancellationToken);
        return CalendarMapper.ToResponse(CalendarProjection.Days(data, today)[0]);
    }
}
