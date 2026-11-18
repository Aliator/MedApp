using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Sites.Commands.SetRoomArchived;
using MedApp.Domain.Sites;
using Moq;
using static MedApp.UnitTests.Common.Constants.SitesTestConstants;

namespace MedApp.UnitTests.Application.Sites.Commands.SetRoomArchived;

[TestFixture]
public sealed class SetRoomArchivedHandlerTests : SiteHandlerTestBase
{
    private SetRoomArchivedHandler Handler => Fixture.Create<SetRoomArchivedHandler>();

    [TestCase(false, true)]
    [TestCase(true, false)]
    public async Task Handle_ChangesTheFlag_SavesIt(bool wasArchived, bool archive)
    {
        var room = NewRoom(isArchived: wasArchived);
        GivenSites(NewSite());
        GivenRooms(room);
        RoomsInUse.Add(RoomId);

        var response = await Handler.Handle(new SetRoomArchivedCommand(RoomId, archive), Token);

        room.IsArchived.Should().Be(archive);
        room.LastUpdated.Should().Be(Now);
        response.IsArchived.Should().Be(archive);
        response.CanBeDeleted.Should().BeFalse();
        Repository.Verify(r => r.UpdateRoomAsync(room, Token), Times.Once);
    }

    [Test]
    public async Task Handle_RestoreAtAnArchivedSite_ThrowsConflict()
    {
        GivenSites(NewSite(isArchived: true));
        GivenRooms(NewRoom(isArchived: true));

        var act = () => Handler.Handle(new SetRoomArchivedCommand(RoomId, false), Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage($"{SiteName} is archived. Restore it first.");
        Repository.Verify(r => r.UpdateRoomAsync(It.IsAny<Room>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_AlreadyAsAsked_SavesNothing()
    {
        GivenSites(NewSite(isArchived: true));
        GivenRooms(NewRoom(isArchived: true));

        var response = await Handler.Handle(new SetRoomArchivedCommand(RoomId, true), Token);

        response.IsArchived.Should().BeTrue();
        Repository.Verify(r => r.UpdateRoomAsync(It.IsAny<Room>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_UnknownRoom_ThrowsNotFound()
    {
        var act = () => Handler.Handle(new SetRoomArchivedCommand(RoomId, true), Token);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
