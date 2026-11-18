using MedApp.Application.Common.Exceptions;
using MedApp.Application.Sites.Common;
using MedApp.Application.Sites.Repositories;
using MediatR;

namespace MedApp.Application.Sites.Commands.DeleteSite;

public sealed class DeleteSiteHandler(ISiteRepository repository) : IRequestHandler<DeleteSiteCommand>
{
    public async Task Handle(DeleteSiteCommand request, CancellationToken cancellationToken)
    {
        var site = await repository.GetSiteOrThrowAsync(request.SiteId, cancellationToken);

        var inUse = await repository.GetSitesInUseAsync([site.Id], cancellationToken);
        if (inUse.Contains(site.Id))
            throw new ConflictException(SiteRules.SiteInUse(site));

        SiteRules.EnsureNotLastOpenSite(site, await repository.CountOpenSitesAsync(cancellationToken));

        await repository.DeleteSiteAsync(site.Id, cancellationToken);
    }
}
