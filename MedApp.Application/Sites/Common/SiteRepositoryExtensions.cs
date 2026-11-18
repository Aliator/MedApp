using MedApp.Application.Common.Exceptions;
using MedApp.Application.Sites.Repositories;
using MedApp.Domain.Sites;

namespace MedApp.Application.Sites.Common;

public static class SiteRepositoryExtensions
{
    /// <summary>The site, or a 404.</summary>
    public static async Task<Site> GetSiteOrThrowAsync(this ISiteRepository repository, Guid id, CancellationToken ct) =>
        await repository.GetSiteAsync(id, ct) ?? throw new NotFoundException($"Site '{id}' was not found.");

    /// <summary>The room, or a 404.</summary>
    public static async Task<Room> GetRoomOrThrowAsync(this ISiteRepository repository, Guid id, CancellationToken ct) =>
        await repository.GetRoomAsync(id, ct) ?? throw new NotFoundException($"Room '{id}' was not found.");

    /// <summary>A site something new may name (a room, a closed day): a 404 if it doesn't exist, a 409 if it's archived.</summary>
    public static async Task<Site> GetOpenSiteOrThrowAsync(this ISiteRepository repository, Guid id, CancellationToken ct)
    {
        var site = await repository.GetSiteOrThrowAsync(id, ct);
        return site.IsArchived ? throw new ConflictException(SiteRules.SiteArchived(site)) : site;
    }
}
