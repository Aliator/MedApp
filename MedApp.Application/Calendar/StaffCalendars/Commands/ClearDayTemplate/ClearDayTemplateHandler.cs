using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Exceptions;
using MediatR;

namespace MedApp.Application.Calendar.StaffCalendars.Commands.ClearDayTemplate;

public sealed class ClearDayTemplateHandler(ICalendarRepository repository, TimeProvider timeProvider)
    : IRequestHandler<ClearDayTemplateCommand>
{
    public async Task Handle(ClearDayTemplateCommand request, CancellationToken cancellationToken)
    {
        request.Actor.EnsureCanChange(request.StaffUserId);
        CalendarGuards.EnsureNotPast(request.Date, UkTime.Today(timeProvider));

        var dayOverride = await repository.GetDayOverrideForUpdateAsync(request.StaffUserId, request.Date, cancellationToken)
                          ?? throw new NotFoundException("This day has no day template of its own.");

        await repository.DeleteDayOverrideAsync(dayOverride, cancellationToken);
    }
}
