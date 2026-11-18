using MedApp.Application.Sites.Common;
using MedApp.Application.Sites.Repositories;
using MedApp.Contracts.Sites.Responses;
using MediatR;

namespace MedApp.Application.Sites.Queries.GetSites;

public sealed class GetSitesHandler(ISiteRepository repository)
    : IRequestHandler<GetSitesQuery, IReadOnlyList<SiteResponse>>
{
    public async Task<IReadOnlyList<SiteResponse>> Handle(GetSitesQuery request, CancellationToken cancellationToken)
    {
        var sites = await repository.GetSitesAsync(request.IncludeArchived, cancellationToken);
        return await SiteMapper.ToResponsesAsync(sites, request.IncludeArchived, repository, cancellationToken);
    }
}
