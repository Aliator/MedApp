using MedApp.Application.Sites.Common;
using MedApp.Application.Sites.Repositories;
using MedApp.Contracts.Sites.Responses;
using MediatR;

namespace MedApp.Application.Sites.Queries.GetRooms;

public sealed class GetRoomsHandler(ISiteRepository repository)
    : IRequestHandler<GetRoomsQuery, IReadOnlyList<RoomResponse>>
{
    public async Task<IReadOnlyList<RoomResponse>> Handle(GetRoomsQuery request, CancellationToken cancellationToken)
    {
        if (request.SiteId is { } siteId)
            await repository.GetSiteOrThrowAsync(siteId, cancellationToken);

        var sites = (await repository.GetSitesAsync(request.IncludeArchived, cancellationToken)).ToDictionary(s => s.Id);
        var rooms = (await repository.GetRoomsAsync(request.SiteId, request.IncludeArchived, cancellationToken))
            .Where(r => sites.ContainsKey(r.SiteId))
            .ToList();
        var inUse = await repository.GetRoomsInUseAsync(rooms.Select(r => r.Id), cancellationToken);

        return rooms
            .Select(r => SiteMapper.ToResponse(r, sites[r.SiteId], inUse.Contains(r.Id)))
            .OrderBy(r => r.SiteName)
            .ThenBy(r => r.Name)
            .ThenBy(r => r.Id)
            .ToList();
    }
}
