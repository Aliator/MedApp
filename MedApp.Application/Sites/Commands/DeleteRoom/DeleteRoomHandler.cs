using MedApp.Application.Common.Exceptions;
using MedApp.Application.Sites.Common;
using MedApp.Application.Sites.Repositories;
using MediatR;

namespace MedApp.Application.Sites.Commands.DeleteRoom;

public sealed class DeleteRoomHandler(ISiteRepository repository) : IRequestHandler<DeleteRoomCommand>
{
    public async Task Handle(DeleteRoomCommand request, CancellationToken cancellationToken)
    {
        var room = await repository.GetRoomOrThrowAsync(request.RoomId, cancellationToken);

        var inUse = await repository.GetRoomsInUseAsync([room.Id], cancellationToken);
        if (inUse.Contains(room.Id))
            throw new ConflictException(SiteRules.RoomInUse(room));

        await repository.DeleteRoomAsync(room.Id, cancellationToken);
    }
}
