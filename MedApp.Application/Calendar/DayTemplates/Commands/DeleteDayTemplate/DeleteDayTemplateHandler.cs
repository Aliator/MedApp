using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Exceptions;
using MediatR;

namespace MedApp.Application.Calendar.DayTemplates.Commands.DeleteDayTemplate;

public sealed class DeleteDayTemplateHandler(ICalendarSetupRepository repository, TimeProvider timeProvider)
    : IRequestHandler<DeleteDayTemplateCommand>
{
    public async Task Handle(DeleteDayTemplateCommand request, CancellationToken cancellationToken)
    {
        var today = UkTime.Today(timeProvider);
        var template = await repository.GetDayTemplateOrThrowAsync(request.DayTemplateId, today, cancellationToken);

        var usage = (await repository.GetDayTemplateUsageAsync([template.Id], today, cancellationToken))
            .GetValueOrDefault(template.Id, DayTemplateUsage.None);
        if (usage.HasBeenUsed)
            throw new ConflictException(
                $"The '{template.Name}' day template has been used by week templates or days, so it can't be deleted. Archive it instead.");

        await repository.DeleteDayTemplateAsync(template.Id, cancellationToken);
    }
}
