using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Sites.Queries.GetRooms;
using MedApp.Application.Sites.Queries.GetSiteById;
using MedApp.Application.Sites.Queries.GetSites;
using Moq;
using static MedApp.UnitTests.Common.Constants.SitesTestConstants;

namespace MedApp.UnitTests.Application.Sites.Queries;

[TestFixture]
public sealed class SiteQueriesHandlerTests : SiteHandlerTestBase
{
    private const string ArchivedSiteName = "Archived annexe";
    private static readonly Guid ArchivedSiteId = Guid.Parse("5173e000-0000-4000-8000-0000000000a3");
    private static readonly Guid ArchivedRoomId = Guid.Parse("5173e000-0000-4000-8000-0000000000b3");
    private static readonly Guid AnnexeRoomId = Guid.Parse("5173e000-0000-4000-8000-0000000000b4");

    [SetUp]
    public void GivenTwoOpenSitesAndAnArchivedOne()
    {
        GivenSites(NewSite(), NewSite(OtherSiteId, OtherSiteName), NewSite(ArchivedSiteId, ArchivedSiteName, isArchived: true));
        GivenRooms(
            NewRoom(),
            NewRoom(ArchivedRoomId, name: "Old room", isArchived: true),
            NewRoom(OtherRoomId, OtherSiteId, OtherRoomName),
            NewRoom(AnnexeRoomId, ArchivedSiteId, "Annexe room"));
        SitesInUse.Add(SiteId);
        RoomsInUse.Add(OtherRoomId);
    }

    [Test]
    public async Task GetSites_OpenOnly_ListsOpenSitesWithTheirOpenRooms()
    {
        var response = await Fixture.Create<GetSitesHandler>().Handle(new GetSitesQuery(), Token);

        response.Select(s => s.Name).Should().Equal(SiteName, OtherSiteName);
        response[0].Rooms.Select(r => r.Name).Should().Equal(RoomName);
        response[0].CanBeDeleted.Should().BeFalse();
        response[1].Rooms.Should().ContainSingle().Which.CanBeDeleted.Should().BeFalse();
        response[1].CanBeDeleted.Should().BeTrue();
        Repository.Verify(r => r.GetSitesAsync(false, Token), Times.Once);
        Repository.Verify(r => r.GetRoomsAsync(null, false, Token), Times.Once);
    }

    [Test]
    public async Task GetSites_WithArchived_ListsEverything()
    {
        var response = await Fixture.Create<GetSitesHandler>().Handle(new GetSitesQuery(IncludeArchived: true), Token);

        response.Select(s => s.Name).Should().Equal(ArchivedSiteName, SiteName, OtherSiteName);
        response.Single(s => s.Id == SiteId).Rooms.Select(r => r.Name).Should().Equal("Old room", RoomName);
        response.Single(s => s.Id == ArchivedSiteId).IsArchived.Should().BeTrue();
    }

    [Test]
    public async Task GetSiteById_ReturnsItWithEveryRoom()
    {
        var response = await Fixture.Create<GetSiteByIdHandler>().Handle(new GetSiteByIdQuery(SiteId), Token);

        response.Name.Should().Be(SiteName);
        response.Rooms.Select(r => r.Name).Should().Equal("Old room", RoomName);
        Repository.Verify(r => r.GetRoomsAsync(SiteId, true, Token), Times.Once);
    }

    [Test]
    public async Task GetSiteById_Unknown_ThrowsNotFound()
    {
        var act = () => Fixture.Create<GetSiteByIdHandler>().Handle(new GetSiteByIdQuery(Guid.NewGuid()), Token);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task GetRooms_EverySite_ListsOpenRoomsOfOpenSitesBySiteThenName()
    {
        var response = await Fixture.Create<GetRoomsHandler>().Handle(new GetRoomsQuery(null), Token);

        response.Select(r => (r.SiteName, r.Name)).Should().Equal((SiteName, RoomName), (OtherSiteName, OtherRoomName));
        response.Single(r => r.Id == OtherRoomId).CanBeDeleted.Should().BeFalse();
    }

    [Test]
    public async Task GetRooms_WithArchived_ListsArchivedSitesRoomsToo()
    {
        var response = await Fixture.Create<GetRoomsHandler>().Handle(new GetRoomsQuery(null, IncludeArchived: true), Token);

        response.Select(r => r.Name).Should().Equal("Annexe room", "Old room", RoomName, OtherRoomName);
    }

    [Test]
    public async Task GetRooms_OneSite_ListsItsRooms()
    {
        var response = await Fixture.Create<GetRoomsHandler>().Handle(new GetRoomsQuery(OtherSiteId), Token);

        response.Should().ContainSingle().Which.Name.Should().Be(OtherRoomName);
        Repository.Verify(r => r.GetRoomsAsync(OtherSiteId, false, Token), Times.Once);
    }

    [Test]
    public async Task GetRooms_UnknownSite_ThrowsNotFound()
    {
        var act = () => Fixture.Create<GetRoomsHandler>().Handle(new GetRoomsQuery(Guid.NewGuid()), Token);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
