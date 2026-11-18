using MedApp.Application.Sites.Common;
using MedApp.Application.Sites.Repositories;
using MedApp.Contracts.Sites.Responses;
using MediatR;

namespace MedApp.Application.Sites.Queries.GetSiteById;

public sealed class GetSiteByIdHandler(ISiteRepository repository) : IRequestHandler<GetSiteByIdQuery, SiteResponse>
{
    public async Task<SiteResponse> Handle(GetSiteByIdQuery request, CancellationToken cancellationToken)
    {
        var site = await repository.GetSiteOrThrowAsync(request.SiteId, cancellationToken);
        return await SiteMapper.ToResponseAsync(site, repository, cancellationToken);
    }
}
