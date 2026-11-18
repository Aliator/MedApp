using MedApp.Application.Common.Exceptions;
using MedApp.Application.Sites.Common;
using MedApp.Application.Sites.Repositories;
using MedApp.Contracts.Sites.Responses;
using MediatR;

namespace MedApp.Application.Sites.Commands.SetRoomArchived;

public sealed class SetRoomArchivedHandler(ISiteRepository repository, TimeProvider timeProvider)
    : IRequestHandler<SetRoomArchivedCommand, RoomResponse>
{
    public async Task<RoomResponse> Handle(SetRoomArchivedCommand request, CancellationToken cancellationToken)
    {
        var room = await repository.GetRoomOrThrowAsync(request.RoomId, cancellationToken);
        var site = await repository.GetSiteOrThrowAsync(room.SiteId, cancellationToken);

        if (room.IsArchived != request.IsArchived)
        {
            if (!request.IsArchived && site.IsArchived)
                throw new ConflictException(SiteRules.SiteArchived(site));

            room.IsArchived = request.IsArchived;
            room.LastUpdated = timeProvider.GetUtcNow().UtcDateTime;
            await repository.UpdateRoomAsync(room, cancellationToken);
        }

        var inUse = await repository.GetRoomsInUseAsync([room.Id], cancellationToken);
        return SiteMapper.ToResponse(room, site, inUse.Contains(room.Id));
    }
}
