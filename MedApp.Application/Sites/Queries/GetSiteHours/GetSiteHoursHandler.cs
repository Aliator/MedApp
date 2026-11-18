using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Sites.Common;
using MedApp.Application.Sites.Repositories;
using MedApp.Contracts.Sites.Responses;
using MediatR;

namespace MedApp.Application.Sites.Queries.GetSiteHours;

public sealed class GetSiteHoursHandler(
    ISiteRepository siteRepository, IWorkingHoursRepository repository, TimeProvider timeProvider)
    : IRequestHandler<GetSiteHoursQuery, SiteHoursResponse>
{
    public async Task<SiteHoursResponse> Handle(GetSiteHoursQuery request, CancellationToken cancellationToken)
    {
        var site = await siteRepository.GetSiteOrThrowAsync(request.SiteId, cancellationToken);
        return WorkingHoursMapper.ToResponse(
            site.Id, await repository.GetSiteHoursOnAsync(site.Id, UkTime.Today(timeProvider), cancellationToken));
    }
}
