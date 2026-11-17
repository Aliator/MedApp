using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Contracts.Calendar.Responses;
using MedApp.Domain.Calendar;
using MediatR;

namespace MedApp.Application.Calendar.StaffCalendars.Commands.SetStaffWeekTemplate;

public sealed class SetStaffWeekTemplateHandler(
    ICalendarRepository repository,
    ICalendarSetupRepository setupRepository,
    ICalendarLock calendarLock,
    IIdentityReadService identityReadService,
    TimeProvider timeProvider)
    : IRequestHandler<SetStaffWeekTemplateCommand, StaffWeekTemplateResponse>
{
    public async Task<StaffWeekTemplateResponse> Handle(SetStaffWeekTemplateCommand request, CancellationToken cancellationToken)
    {
        request.Actor.EnsureCanChange(request.StaffUserId);
        await identityReadService.GetUsernameOrThrowAsync(request.StaffUserId, cancellationToken);

        var today = UkTime.Today(timeProvider);
        var template = request.WeekTemplateId is { } weekTemplateId
            ? await setupRepository.GetOwnWeekTemplateOrThrowAsync(weekTemplateId, request.StaffUserId, today, cancellationToken)
            : null;

        // Under the calendar lock, so two changes at once can't both add a row from today.
        await calendarLock.RunAsync([request.StaffUserId], async ct =>
        {
            var rows = await repository.GetStaffWeekTemplatesForUpdateAsync(request.StaffUserId, today, ct);

            // An archived one stays someone's default until it's changed, but can't be chosen again.
            if (template is { IsArchived: true } && !rows.Any(r => r.WeekTemplateId == template.Id && r.IsValidOn(today)))
                throw new ConflictException($"The '{template.Name}' week template is archived, so it can't be chosen.");

            var change = TemplateHistory.Replace(
                rows,
                template is null ? [] : [template.Id],
                today,
                r => r.WeekTemplateId,
                (weekTemplateId, validFrom) => new StaffWeekTemplate
                {
                    Id = Guid.NewGuid(),
                    StaffUserId = request.StaffUserId,
                    WeekTemplateId = weekTemplateId,
                    ValidFrom = validFrom
                });

            if (change.HasChanges)
                await repository.SaveStaffWeekTemplatesAsync(change, ct);

            return change.HasChanges;
        }, cancellationToken);

        var defaultWeek = await repository.GetStaffWeekTemplateOnAsync(request.StaffUserId, today, cancellationToken);
        return CalendarMapper.ToResponse(request.StaffUserId, defaultWeek);
    }
}
