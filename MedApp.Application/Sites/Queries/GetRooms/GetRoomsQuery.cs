using MedApp.Contracts.Sites.Responses;
using MediatR;

namespace MedApp.Application.Sites.Queries.GetRooms;

/// <summary>
/// Rooms by site name, then room name: one site's (a 404 if there's no such site), or every site's. Archived rooms,
/// and the rooms of archived sites, only when asked for.
/// </summary>
public sealed record GetRoomsQuery(Guid? SiteId, bool IncludeArchived = false) : IRequest<IReadOnlyList<RoomResponse>>;
