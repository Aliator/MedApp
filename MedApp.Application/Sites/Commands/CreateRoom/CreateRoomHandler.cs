using MedApp.Application.Common.Exceptions;
using MedApp.Application.Sites.Common;
using MedApp.Application.Sites.Repositories;
using MedApp.Contracts.Sites.Responses;
using MedApp.Domain.Sites;
using MediatR;

namespace MedApp.Application.Sites.Commands.CreateRoom;

public sealed class CreateRoomHandler(ISiteRepository repository, TimeProvider timeProvider)
    : IRequestHandler<CreateRoomCommand, RoomResponse>
{
    public async Task<RoomResponse> Handle(CreateRoomCommand request, CancellationToken cancellationToken)
    {
        var site = await repository.GetOpenSiteOrThrowAsync(request.SiteId, cancellationToken);

        var name = request.Name.Trim();
        if (await repository.RoomNameExistsAsync(site.Id, name, null, cancellationToken))
            throw new ConflictException(SiteRules.RoomNameTaken(site.Name, name));

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var room = new Room
        {
            Id = Guid.NewGuid(),
            SiteId = site.Id,
            Name = name,
            CreatedAt = now,
            LastUpdated = now
        };

        await repository.AddRoomAsync(room, cancellationToken);

        return SiteMapper.ToResponse(room, site, inUse: false);
    }
}
