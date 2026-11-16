using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Contracts.Calendar.Responses;
using MediatR;

namespace MedApp.Application.Calendar.WeekTemplates.Queries.GetWeekTemplates;

public sealed class GetWeekTemplatesHandler(ICalendarSetupRepository repository, TimeProvider timeProvider)
    : IRequestHandler<GetWeekTemplatesQuery, IReadOnlyList<WeekTemplateResponse>>
{
    public async Task<IReadOnlyList<WeekTemplateResponse>> Handle(GetWeekTemplatesQuery request, CancellationToken cancellationToken)
    {
        var today = UkTime.Today(timeProvider);
        var templates = await repository.GetWeekTemplatesAsync(request.IncludeArchived, today, cancellationToken);
        var usage = await repository.GetWeekTemplateUsageAsync(templates.Select(t => t.Id), today, cancellationToken);

        return templates
            .Select(t => CalendarSetupMapper.ToResponse(t, usage.GetValueOrDefault(t.Id, WeekTemplateUsage.None), today))
            .ToList();
    }
}
