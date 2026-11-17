using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Contracts.Calendar.Responses;
using MediatR;

namespace MedApp.Application.Calendar.DayTemplates.Commands.SetDayTemplateArchived;

public sealed class SetDayTemplateArchivedHandler(ICalendarSetupRepository repository, TimeProvider timeProvider)
    : IRequestHandler<SetDayTemplateArchivedCommand, DayTemplateResponse>
{
    public async Task<DayTemplateResponse> Handle(SetDayTemplateArchivedCommand request, CancellationToken cancellationToken)
    {
        var today = UkTime.Today(timeProvider);
        var template = await repository.GetDayTemplateForUpdateOrThrowAsync(request.DayTemplateId, today, cancellationToken);
        request.Actor.EnsureCanChangeTemplatesOf(template.OwnerUserId);

        if (template.IsArchived != request.IsArchived)
        {
            template.IsArchived = request.IsArchived;
            await repository.SaveDayTemplateAsync(template, null, cancellationToken);
        }

        return await CalendarSetupMapper.ToResponseAsync(template, repository, today, canEdit: true, cancellationToken);
    }
}
