using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MediatR;

namespace MedApp.Application.Calendar.StaffCalendars.Commands.RemoveTemplateBlockForDay;

public sealed class RemoveTemplateBlockForDayHandler(
    ICalendarRepository repository,
    ICalendarLock calendarLock,
    TimeProvider timeProvider)
    : IRequestHandler<RemoveTemplateBlockForDayCommand>
{
    public async Task Handle(RemoveTemplateBlockForDayCommand request, CancellationToken cancellationToken)
    {
        request.Actor.EnsureCanChange(request.StaffUserId);
        CalendarGuards.EnsureNotPast(request.Date, UkTime.Today(timeProvider));

        await calendarLock.RunAsync([request.StaffUserId], async ct =>
        {
            var data = await repository.GetCalendarDataAsync(request.StaffUserId, request.Date, request.Date, ct);
            var templateBlock = TemplateBlockForDay.Find(data, request.TemplateBlockId, request.Date);

            await repository.AddBlockExceptionAsync(
                TemplateBlockForDay.Exception(request.StaffUserId, request.Date, templateBlock), null, ct);
            return true;
        }, cancellationToken);
    }
}
