using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Sites.Commands.CreateRoom;
using MedApp.Domain.Sites;
using Moq;
using static MedApp.UnitTests.Common.Constants.SitesTestConstants;

namespace MedApp.UnitTests.Application.Sites.Commands.CreateRoom;

[TestFixture]
public sealed class CreateRoomHandlerTests : SiteHandlerTestBase
{
    private CreateRoomHandler Handler => Fixture.Create<CreateRoomHandler>();

    [Test]
    public async Task Handle_NewName_AddsTheRoomAtTheSite()
    {
        GivenSites(NewSite());
        GivenRooms(NewRoom(OtherRoomId, OtherSiteId, RoomName));

        var response = await Handler.Handle(new CreateRoomCommand(SiteId, $" {RoomName} "), Token);

        var room = Rooms.Should().ContainSingle(r => r.SiteId == SiteId).Subject;
        room.Name.Should().Be(RoomName);
        room.IsArchived.Should().BeFalse();
        room.CreatedAt.Should().Be(Now);
        room.LastUpdated.Should().Be(Now);
        Repository.Verify(r => r.AddRoomAsync(room, Token), Times.Once);

        response.Id.Should().Be(room.Id);
        response.SiteId.Should().Be(SiteId);
        response.SiteName.Should().Be(SiteName);
        response.Name.Should().Be(RoomName);
        response.CanBeDeleted.Should().BeTrue();
    }

    [Test]
    public async Task Handle_NameTakenAtTheSite_ThrowsConflict()
    {
        GivenSites(NewSite());
        GivenRooms(NewRoom(isArchived: true));

        var act = () => Handler.Handle(new CreateRoomCommand(SiteId, RoomName.ToLowerInvariant()), Token);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage($"{SiteName} already has a room called '{RoomName.ToLowerInvariant()}'.");
        Repository.Verify(r => r.AddRoomAsync(It.IsAny<Room>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_ArchivedSite_ThrowsConflict()
    {
        GivenSites(NewSite(isArchived: true));

        var act = () => Handler.Handle(new CreateRoomCommand(SiteId, RoomName), Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage($"{SiteName} is archived. Restore it first.");
    }

    [Test]
    public async Task Handle_UnknownSite_ThrowsNotFound()
    {
        var act = () => Handler.Handle(new CreateRoomCommand(SiteId, RoomName), Token);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
