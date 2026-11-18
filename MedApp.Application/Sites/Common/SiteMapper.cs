using MedApp.Application.Sites.Repositories;
using MedApp.Contracts.Sites.Responses;
using MedApp.Domain.Sites;

namespace MedApp.Application.Sites.Common;

/// <summary>Sites and rooms as the API returns them.</summary>
public static class SiteMapper
{
    /// <param name="site">The site.</param>
    /// <param name="rooms">Rooms to include: those at this site are, by name.</param>
    /// <param name="inUse">Whether something names it (a room or a closed day).</param>
    /// <param name="isLastOpen">Whether it's the only site that isn't archived.</param>
    /// <param name="roomsInUse">Which rooms something names.</param>
    public static SiteResponse ToResponse(
        Site site,
        IEnumerable<Room> rooms,
        bool inUse,
        bool isLastOpen,
        IReadOnlySet<Guid> roomsInUse) => new()
    {
        Id = site.Id,
        Name = site.Name,
        AddressLines = SiteRules.SplitAddress(site.AddressLines),
        Postcode = site.Postcode,
        Phone = site.Phone,
        IsArchived = site.IsArchived,
        CanBeDeleted = !inUse && !isLastOpen,
        Rooms = rooms
            .Where(r => r.SiteId == site.Id)
            .OrderBy(r => r.Name)
            .ThenBy(r => r.Id)
            .Select(r => ToResponse(r, site, roomsInUse.Contains(r.Id)))
            .ToList(),
        CreatedAt = site.CreatedAt,
        LastUpdated = site.LastUpdated
    };

    public static RoomResponse ToResponse(Room room, Site site, bool inUse) => new()
    {
        Id = room.Id,
        SiteId = room.SiteId,
        SiteName = site.Name,
        Name = room.Name,
        IsArchived = room.IsArchived,
        CanBeDeleted = !inUse
    };

    /// <summary>Sites with their rooms, looking up what's in use and how many sites are open.</summary>
    public static async Task<IReadOnlyList<SiteResponse>> ToResponsesAsync(
        IReadOnlyList<Site> sites,
        bool includeArchivedRooms,
        ISiteRepository repository,
        CancellationToken ct)
    {
        var rooms = await repository.GetRoomsAsync(sites.Count == 1 ? sites[0].Id : null, includeArchivedRooms, ct);
        var sitesInUse = await repository.GetSitesInUseAsync(sites.Select(s => s.Id), ct);
        var roomsInUse = await repository.GetRoomsInUseAsync(rooms.Select(r => r.Id), ct);
        var openSites = await repository.CountOpenSitesAsync(ct);

        return sites
            .Select(s => ToResponse(s, rooms, sitesInUse.Contains(s.Id), !s.IsArchived && openSites <= 1, roomsInUse))
            .ToList();
    }

    /// <summary>One site with every room it has, archived ones too.</summary>
    public static async Task<SiteResponse> ToResponseAsync(Site site, ISiteRepository repository, CancellationToken ct) =>
        (await ToResponsesAsync([site], includeArchivedRooms: true, repository, ct))[0];

    /// <summary>One room, looking up its site and whether it's in use.</summary>
    public static async Task<RoomResponse> ToResponseAsync(Room room, ISiteRepository repository, CancellationToken ct)
    {
        var site = await repository.GetSiteOrThrowAsync(room.SiteId, ct);
        var inUse = await repository.GetRoomsInUseAsync([room.Id], ct);
        return ToResponse(room, site, inUse.Contains(room.Id));
    }
}
