using MedApp.Application.Common.Exceptions;
using MedApp.Application.Sites.Common;
using MedApp.Application.Sites.Repositories;
using MedApp.Contracts.Sites.Responses;
using MediatR;

namespace MedApp.Application.Sites.Commands.UpdateSite;

public sealed class UpdateSiteHandler(ISiteRepository repository, TimeProvider timeProvider)
    : IRequestHandler<UpdateSiteCommand, SiteResponse>
{
    public async Task<SiteResponse> Handle(UpdateSiteCommand request, CancellationToken cancellationToken)
    {
        var site = await repository.GetSiteOrThrowAsync(request.SiteId, cancellationToken);

        var name = request.Name.Trim();
        if (name != site.Name && await repository.SiteNameExistsAsync(name, site.Id, cancellationToken))
            throw new ConflictException(SiteRules.NameTaken(name));

        site.Name = name;
        site.AddressLines = SiteRules.JoinAddress(request.AddressLines);
        site.Postcode = SiteRules.Postcode(request.Postcode);
        site.Phone = SiteRules.Optional(request.Phone);
        site.LastUpdated = timeProvider.GetUtcNow().UtcDateTime;

        await repository.UpdateSiteAsync(site, cancellationToken);

        return await SiteMapper.ToResponseAsync(site, repository, cancellationToken);
    }
}
