using MediatR;

namespace MedApp.Application.Sites.Commands.DeleteRoom;

/// <summary>Deletes a room nothing names; one that's been used is archived instead (409).</summary>
public sealed record DeleteRoomCommand(Guid RoomId) : IRequest;
