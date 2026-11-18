using MedApp.Application.Common.Exceptions;
using MedApp.Application.Sites.Common;
using MedApp.Application.Sites.Repositories;
using MedApp.Contracts.Sites.Responses;
using MediatR;

namespace MedApp.Application.Sites.Commands.UpdateRoom;

public sealed class UpdateRoomHandler(ISiteRepository repository, TimeProvider timeProvider)
    : IRequestHandler<UpdateRoomCommand, RoomResponse>
{
    public async Task<RoomResponse> Handle(UpdateRoomCommand request, CancellationToken cancellationToken)
    {
        var room = await repository.GetRoomOrThrowAsync(request.RoomId, cancellationToken);
        var site = await repository.GetSiteOrThrowAsync(room.SiteId, cancellationToken);

        var name = request.Name.Trim();
        if (name != room.Name && await repository.RoomNameExistsAsync(site.Id, name, room.Id, cancellationToken))
            throw new ConflictException(SiteRules.RoomNameTaken(site.Name, name));

        room.Name = name;
        room.LastUpdated = timeProvider.GetUtcNow().UtcDateTime;
        await repository.UpdateRoomAsync(room, cancellationToken);

        var inUse = await repository.GetRoomsInUseAsync([room.Id], cancellationToken);
        return SiteMapper.ToResponse(room, site, inUse.Contains(room.Id));
    }
}
