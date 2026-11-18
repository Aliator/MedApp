using MedApp.Contracts.Sites.Responses;
using MediatR;

namespace MedApp.Application.Sites.Commands.CreateRoom;

/// <summary>Adds a room to a site that isn't archived, with a name unique within the site.</summary>
public sealed record CreateRoomCommand(Guid SiteId, string Name) : IRequest<RoomResponse>;
