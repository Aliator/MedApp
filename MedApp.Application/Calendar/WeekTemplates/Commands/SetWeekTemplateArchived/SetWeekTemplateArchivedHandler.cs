using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Contracts.Calendar.Responses;
using MediatR;

namespace MedApp.Application.Calendar.WeekTemplates.Commands.SetWeekTemplateArchived;

public sealed class SetWeekTemplateArchivedHandler(ICalendarSetupRepository repository, TimeProvider timeProvider)
    : IRequestHandler<SetWeekTemplateArchivedCommand, WeekTemplateResponse>
{
    public async Task<WeekTemplateResponse> Handle(SetWeekTemplateArchivedCommand request, CancellationToken cancellationToken)
    {
        var today = UkTime.Today(timeProvider);
        var template = await repository.GetWeekTemplateForUpdateOrThrowAsync(request.WeekTemplateId, today, cancellationToken);
        request.Actor.EnsureCanChangeTemplatesOf(template.OwnerUserId);

        if (template.IsArchived != request.IsArchived)
        {
            template.IsArchived = request.IsArchived;
            await repository.SaveWeekTemplateAsync(template, null, cancellationToken);
        }

        return await CalendarSetupMapper.ToResponseAsync(template, repository, today, canEdit: true, cancellationToken);
    }
}
