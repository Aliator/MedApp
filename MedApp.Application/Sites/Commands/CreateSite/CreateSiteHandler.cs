using MedApp.Application.Common.Exceptions;
using MedApp.Application.Sites.Common;
using MedApp.Application.Sites.Repositories;
using MedApp.Contracts.Sites.Responses;
using MedApp.Domain.Sites;
using MediatR;

namespace MedApp.Application.Sites.Commands.CreateSite;

public sealed class CreateSiteHandler(ISiteRepository repository, TimeProvider timeProvider)
    : IRequestHandler<CreateSiteCommand, SiteResponse>
{
    public async Task<SiteResponse> Handle(CreateSiteCommand request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (await repository.SiteNameExistsAsync(name, null, cancellationToken))
            throw new ConflictException(SiteRules.NameTaken(name));

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var site = new Site
        {
            Id = Guid.NewGuid(),
            Name = name,
            AddressLines = SiteRules.JoinAddress(request.AddressLines),
            Postcode = SiteRules.Postcode(request.Postcode),
            Phone = SiteRules.Optional(request.Phone),
            CreatedAt = now,
            LastUpdated = now
        };

        await repository.AddSiteAsync(site, cancellationToken);

        return await SiteMapper.ToResponseAsync(site, repository, cancellationToken);
    }
}
