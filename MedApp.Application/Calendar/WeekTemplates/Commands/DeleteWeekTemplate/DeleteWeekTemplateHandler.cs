using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Exceptions;
using MediatR;

namespace MedApp.Application.Calendar.WeekTemplates.Commands.DeleteWeekTemplate;

public sealed class DeleteWeekTemplateHandler(ICalendarSetupRepository repository, TimeProvider timeProvider)
    : IRequestHandler<DeleteWeekTemplateCommand>
{
    public async Task Handle(DeleteWeekTemplateCommand request, CancellationToken cancellationToken)
    {
        var today = UkTime.Today(timeProvider);
        var template = await repository.GetWeekTemplateOrThrowAsync(request.WeekTemplateId, today, cancellationToken);
        request.Actor.EnsureCanChangeTemplatesOf(template.OwnerUserId);

        var usage = (await repository.GetWeekTemplateUsageAsync([template.Id], today, cancellationToken))
            .GetValueOrDefault(template.Id, WeekTemplateUsage.None);
        if (usage.HasBeenUsed)
            throw new ConflictException(
                $"The '{template.Name}' week template has been someone's default week, so it can't be deleted. Archive it instead.");

        await repository.DeleteWeekTemplateAsync(template.Id, cancellationToken);
    }
}
