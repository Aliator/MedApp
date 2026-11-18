using MedApp.Domain.Sites;

namespace MedApp.Application.Sites.Repositories;

/// <summary>Sites and their rooms. Reads are read-only; each change saves itself.</summary>
public interface ISiteRepository
{
    /// <summary>Every site by name; archived ones only when asked for.</summary>
    Task<IReadOnlyList<Site>> GetSitesAsync(bool includeArchived, CancellationToken ct);

    Task<Site?> GetSiteAsync(Guid id, CancellationToken ct);

    /// <summary>Rooms by name: one site's, or every site's; archived ones only when asked for.</summary>
    Task<IReadOnlyList<Room>> GetRoomsAsync(Guid? siteId, bool includeArchived, CancellationToken ct);

    Task<Room?> GetRoomAsync(Guid id, CancellationToken ct);

    /// <summary>Whether another site has this name (ignoring case).</summary>
    Task<bool> SiteNameExistsAsync(string name, Guid? exceptId, CancellationToken ct);

    /// <summary>Whether another room at the site has this name (ignoring case).</summary>
    Task<bool> RoomNameExistsAsync(Guid siteId, string name, Guid? exceptId, CancellationToken ct);

    /// <summary>How many sites aren't archived.</summary>
    Task<int> CountOpenSitesAsync(CancellationToken ct);

    /// <summary>Of these sites, those something names (a room or a closed day), so they can't be deleted.</summary>
    Task<IReadOnlySet<Guid>> GetSitesInUseAsync(IEnumerable<Guid> ids, CancellationToken ct);

    /// <summary>
    /// Of these rooms, those something names, so they can't be deleted. Nothing names a room yet: clinic sessions and
    /// appointments will (the centre plan's PR 23).
    /// </summary>
    Task<IReadOnlySet<Guid>> GetRoomsInUseAsync(IEnumerable<Guid> ids, CancellationToken ct);

    /// <summary>Adds a site. Another site with its name (added just now) is a <see cref="Common.Exceptions.ConflictException"/>.</summary>
    Task AddSiteAsync(Site site, CancellationToken ct);

    /// <summary>Saves a site's changes. Another site with its name is a <see cref="Common.Exceptions.ConflictException"/>.</summary>
    Task UpdateSiteAsync(Site site, CancellationToken ct);

    Task DeleteSiteAsync(Guid id, CancellationToken ct);

    /// <summary>Adds a room. Another room at the site with its name is a <see cref="Common.Exceptions.ConflictException"/>.</summary>
    Task AddRoomAsync(Room room, CancellationToken ct);

    /// <summary>Saves a room's changes. Another room at the site with its name is a <see cref="Common.Exceptions.ConflictException"/>.</summary>
    Task UpdateRoomAsync(Room room, CancellationToken ct);

    Task DeleteRoomAsync(Guid id, CancellationToken ct);
}
