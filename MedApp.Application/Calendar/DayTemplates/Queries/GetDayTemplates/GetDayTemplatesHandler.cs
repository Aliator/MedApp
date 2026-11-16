using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Identity;
using MedApp.Contracts.Calendar.Responses;
using MediatR;

namespace MedApp.Application.Calendar.DayTemplates.Queries.GetDayTemplates;

public sealed class GetDayTemplatesHandler(
    ICalendarSetupRepository repository,
    IIdentityReadService identityReadService,
    TimeProvider timeProvider)
    : IRequestHandler<GetDayTemplatesQuery, IReadOnlyList<DayTemplateResponse>>
{
    public async Task<IReadOnlyList<DayTemplateResponse>> Handle(GetDayTemplatesQuery request, CancellationToken cancellationToken)
    {
        await identityReadService.GetUsernameOrThrowAsync(request.OwnerUserId, cancellationToken);

        var today = UkTime.Today(timeProvider);
        var templates = await repository.GetDayTemplatesAsync(request.OwnerUserId, request.IncludeArchived, today, cancellationToken);
        var usage = await repository.GetDayTemplateUsageAsync(templates.Select(t => t.Id), today, cancellationToken);
        var canEdit = request.Actor.CanChangeCalendarOf(request.OwnerUserId);

        return templates
            .Select(t => CalendarSetupMapper.ToResponse(t, usage.GetValueOrDefault(t.Id, DayTemplateUsage.None), today, canEdit))
            .ToList();
    }
}
