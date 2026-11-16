using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Contracts.Calendar.Responses;
using MediatR;

namespace MedApp.Application.Calendar.WeekTemplates.Commands.UpdateWeekTemplate;

public sealed class UpdateWeekTemplateHandler(ICalendarSetupRepository repository, TimeProvider timeProvider)
    : IRequestHandler<UpdateWeekTemplateCommand, WeekTemplateResponse>
{
    public async Task<WeekTemplateResponse> Handle(UpdateWeekTemplateCommand request, CancellationToken cancellationToken)
    {
        var today = UkTime.Today(timeProvider);
        var template = await repository.GetWeekTemplateForUpdateOrThrowAsync(request.WeekTemplateId, today, cancellationToken);

        var name = request.Name.Trim();
        if (name != template.Name)
        {
            if (await repository.WeekTemplateNameExistsAsync(name, template.Id, cancellationToken))
                throw new ConflictException($"A week template called '{name}' already exists.");

            template.Name = name;
        }

        var days = request.Days();
        await TemplateChecks.CheckDayTemplatesAsync(repository, days, TemplateChecks.DayTemplatesIn(template), cancellationToken);

        var change = TemplateHistory.Replace(
            template.Days,
            days,
            today,
            WeekTemplateDayValue.Of,
            (value, validFrom) => value.ToDay(template.Id, validFrom));

        await repository.SaveWeekTemplateAsync(template, change.HasChanges ? change : null, cancellationToken);

        var saved = await repository.GetWeekTemplateOrThrowAsync(template.Id, today, cancellationToken);
        return await CalendarSetupMapper.ToResponseAsync(saved, repository, today, cancellationToken);
    }
}
