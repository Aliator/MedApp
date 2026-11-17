using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Contracts.Calendar.Responses;
using MediatR;

namespace MedApp.Application.Calendar.DayTemplates.Commands.UpdateDayTemplate;

public sealed class UpdateDayTemplateHandler(ICalendarSetupRepository repository, TimeProvider timeProvider)
    : IRequestHandler<UpdateDayTemplateCommand, DayTemplateResponse>
{
    public async Task<DayTemplateResponse> Handle(UpdateDayTemplateCommand request, CancellationToken cancellationToken)
    {
        var today = UkTime.Today(timeProvider);
        var template = await repository.GetDayTemplateForUpdateOrThrowAsync(request.DayTemplateId, today, cancellationToken);
        request.Actor.EnsureCanChangeTemplatesOf(template.OwnerUserId);

        var name = request.Name.Trim();
        if (name != template.Name)
        {
            if (await repository.DayTemplateNameExistsAsync(template.OwnerUserId, name, template.Id, cancellationToken))
                throw new ConflictException($"A day template called '{name}' already exists.");

            template.Name = name;
        }

        await TemplateChecks.CheckActivityTypesAsync(
            repository, request.Blocks, TemplateChecks.ActivityTypesIn(template), cancellationToken);

        var blocks = TemplateHistory.Replace(
            template.Blocks,
            request.Blocks.Select(b => b.Normalised),
            today,
            DayTemplateBlockValue.Of,
            (value, validFrom) => value.ToBlock(template.Id, validFrom));

        await repository.SaveDayTemplateAsync(template, blocks.HasChanges ? blocks : null, cancellationToken);

        var saved = await repository.GetDayTemplateOrThrowAsync(template.Id, today, cancellationToken);
        return await CalendarSetupMapper.ToResponseAsync(saved, repository, today, canEdit: true, cancellationToken);
    }
}
