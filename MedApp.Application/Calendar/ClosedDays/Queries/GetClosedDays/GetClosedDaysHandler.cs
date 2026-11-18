using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Sites.Repositories;
using MedApp.Contracts.Calendar.Responses;
using MediatR;

namespace MedApp.Application.Calendar.ClosedDays.Queries.GetClosedDays;

public sealed class GetClosedDaysHandler(IClosedDayRepository repository, ISiteRepository siteRepository, TimeProvider timeProvider)
    : IRequestHandler<GetClosedDaysQuery, IReadOnlyList<ClosedDayResponse>>
{
    public async Task<IReadOnlyList<ClosedDayResponse>> Handle(GetClosedDaysQuery request, CancellationToken cancellationToken)
    {
        var today = UkTime.Today(timeProvider);
        var days = await repository.GetAsync(request.From, request.To, cancellationToken);

        // Sites' names, archived ones too, as a site's closed days outlive its archiving.
        var sites = days.Any(d => d.SiteId is not null)
            ? (await siteRepository.GetSitesAsync(includeArchived: true, cancellationToken)).ToDictionary(s => s.Id, s => s.Name)
            : new Dictionary<Guid, string>();

        return days
            .Select(d => ClosedDayMapper.ToResponse(d, today, d.SiteId is { } siteId ? sites.GetValueOrDefault(siteId) : null))
            .ToList();
    }
}
