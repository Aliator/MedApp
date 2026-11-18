using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Sites.Commands.DeleteRoom;
using Moq;
using static MedApp.UnitTests.Common.Constants.SitesTestConstants;

namespace MedApp.UnitTests.Application.Sites.Commands.DeleteRoom;

[TestFixture]
public sealed class DeleteRoomHandlerTests : SiteHandlerTestBase
{
    private DeleteRoomHandler Handler => Fixture.Create<DeleteRoomHandler>();

    [Test]
    public async Task Handle_UnusedRoom_DeletesIt()
    {
        GivenRooms(NewRoom());

        await Handler.Handle(new DeleteRoomCommand(RoomId), Token);

        Repository.Verify(r => r.GetRoomsInUseAsync(It.IsAny<IEnumerable<Guid>>(), Token), Times.Once);
        Repository.Verify(r => r.DeleteRoomAsync(RoomId, Token), Times.Once);
    }

    [Test]
    public async Task Handle_UsedRoom_ThrowsConflict()
    {
        GivenRooms(NewRoom());
        RoomsInUse.Add(RoomId);

        var act = () => Handler.Handle(new DeleteRoomCommand(RoomId), Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage($"{RoomName} has been used, so it can't be deleted. Archive it instead.");
        Repository.Verify(r => r.DeleteRoomAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_UnknownRoom_ThrowsNotFound()
    {
        var act = () => Handler.Handle(new DeleteRoomCommand(RoomId), Token);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
