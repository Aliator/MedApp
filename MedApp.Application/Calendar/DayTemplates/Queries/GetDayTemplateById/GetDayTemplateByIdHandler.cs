using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Contracts.Calendar.Responses;
using MediatR;

namespace MedApp.Application.Calendar.DayTemplates.Queries.GetDayTemplateById;

public sealed class GetDayTemplateByIdHandler(ICalendarSetupRepository repository, TimeProvider timeProvider)
    : IRequestHandler<GetDayTemplateByIdQuery, DayTemplateResponse>
{
    public async Task<DayTemplateResponse> Handle(GetDayTemplateByIdQuery request, CancellationToken cancellationToken)
    {
        var today = UkTime.Today(timeProvider);
        var template = await repository.GetDayTemplateOrThrowAsync(request.DayTemplateId, today, cancellationToken);
        return await CalendarSetupMapper.ToResponseAsync(
            template, repository, today, request.Actor.CanChangeCalendarOf(template.OwnerUserId), cancellationToken);
    }
}
