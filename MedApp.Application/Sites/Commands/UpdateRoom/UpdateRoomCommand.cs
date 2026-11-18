using MedApp.Contracts.Sites.Responses;
using MediatR;

namespace MedApp.Application.Sites.Commands.UpdateRoom;

/// <summary>Renames a room; its site never changes.</summary>
public sealed record UpdateRoomCommand(Guid RoomId, string Name) : IRequest<RoomResponse>;
