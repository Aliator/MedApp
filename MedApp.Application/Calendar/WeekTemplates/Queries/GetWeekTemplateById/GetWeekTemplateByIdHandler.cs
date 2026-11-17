using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Contracts.Calendar.Responses;
using MediatR;

namespace MedApp.Application.Calendar.WeekTemplates.Queries.GetWeekTemplateById;

public sealed class GetWeekTemplateByIdHandler(ICalendarSetupRepository repository, TimeProvider timeProvider)
    : IRequestHandler<GetWeekTemplateByIdQuery, WeekTemplateResponse>
{
    public async Task<WeekTemplateResponse> Handle(GetWeekTemplateByIdQuery request, CancellationToken cancellationToken)
    {
        var today = UkTime.Today(timeProvider);
        var template = await repository.GetWeekTemplateOrThrowAsync(request.WeekTemplateId, today, cancellationToken);
        return await CalendarSetupMapper.ToResponseAsync(
            template, repository, today, request.Actor.CanChangeCalendarOf(template.OwnerUserId), cancellationToken);
    }
}
