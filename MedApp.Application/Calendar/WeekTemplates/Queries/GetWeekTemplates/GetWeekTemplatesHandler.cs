using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Identity;
using MedApp.Contracts.Calendar.Responses;
using MediatR;

namespace MedApp.Application.Calendar.WeekTemplates.Queries.GetWeekTemplates;

public sealed class GetWeekTemplatesHandler(
    ICalendarSetupRepository repository,
    IIdentityReadService identityReadService,
    TimeProvider timeProvider)
    : IRequestHandler<GetWeekTemplatesQuery, IReadOnlyList<WeekTemplateResponse>>
{
    public async Task<IReadOnlyList<WeekTemplateResponse>> Handle(GetWeekTemplatesQuery request, CancellationToken cancellationToken)
    {
        await identityReadService.GetUsernameOrThrowAsync(request.OwnerUserId, cancellationToken);

        var today = UkTime.Today(timeProvider);
        var templates = await repository.GetWeekTemplatesAsync(request.OwnerUserId, request.IncludeArchived, today, cancellationToken);
        var usage = await repository.GetWeekTemplateUsageAsync(templates.Select(t => t.Id), today, cancellationToken);
        var canEdit = request.Actor.CanChangeCalendarOf(request.OwnerUserId);

        return templates
            .Select(t => CalendarSetupMapper.ToResponse(t, usage.GetValueOrDefault(t.Id, WeekTemplateUsage.None), today, canEdit))
            .ToList();
    }
}
