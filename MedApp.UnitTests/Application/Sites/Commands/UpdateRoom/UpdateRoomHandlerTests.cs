using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Sites.Commands.UpdateRoom;
using MedApp.Domain.Sites;
using Moq;
using static MedApp.UnitTests.Common.Constants.SitesTestConstants;

namespace MedApp.UnitTests.Application.Sites.Commands.UpdateRoom;

[TestFixture]
public sealed class UpdateRoomHandlerTests : SiteHandlerTestBase
{
    private UpdateRoomHandler Handler => Fixture.Create<UpdateRoomHandler>();

    [Test]
    public async Task Handle_NewName_RenamesIt()
    {
        var room = NewRoom();
        GivenSites(NewSite());
        GivenRooms(room, NewRoom(OtherRoomId, OtherSiteId, OtherRoomName));

        var response = await Handler.Handle(new UpdateRoomCommand(RoomId, $" {OtherRoomName} "), Token);

        room.Name.Should().Be(OtherRoomName);
        room.LastUpdated.Should().Be(Now);
        Repository.Verify(r => r.UpdateRoomAsync(room, Token), Times.Once);
        Repository.Verify(r => r.GetRoomsInUseAsync(It.IsAny<IEnumerable<Guid>>(), Token), Times.Once);
        response.Name.Should().Be(OtherRoomName);
        response.SiteName.Should().Be(SiteName);
    }

    [Test]
    public async Task Handle_AnotherRoomsNameAtTheSite_ThrowsConflict()
    {
        GivenSites(NewSite());
        GivenRooms(NewRoom(), NewRoom(OtherRoomId, name: OtherRoomName));

        var act = () => Handler.Handle(new UpdateRoomCommand(RoomId, OtherRoomName), Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage($"{SiteName} already has a room called '{OtherRoomName}'.");
        Repository.Verify(r => r.UpdateRoomAsync(It.IsAny<Room>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_UnknownRoom_ThrowsNotFound()
    {
        var act = () => Handler.Handle(new UpdateRoomCommand(RoomId, RoomName), Token);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
