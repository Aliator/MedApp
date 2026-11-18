using MedApp.Contracts.Sites.Responses;
using MediatR;

namespace MedApp.Application.Sites.Commands.SetRoomArchived;

/// <summary>
/// Archives a room (it can't be chosen for anything new; what names it keeps it) or restores it. A room at an archived
/// site can't be restored until its site is (409).
/// </summary>
public sealed record SetRoomArchivedCommand(Guid RoomId, bool IsArchived) : IRequest<RoomResponse>;
