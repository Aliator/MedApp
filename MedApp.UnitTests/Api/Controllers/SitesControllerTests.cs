using MedApp.Application.Sites.Queries.GetSiteHours;
using MedApp.Application.Sites.Commands.SetSiteHours;
using AutoFixture;
using FluentAssertions;
using MedApp.API.Controllers;
using MedApp.Application.Sites.Commands.CreateRoom;
using MedApp.Application.Sites.Commands.CreateSite;
using MedApp.Application.Sites.Commands.DeleteRoom;
using MedApp.Application.Sites.Commands.DeleteSite;
using MedApp.Application.Sites.Commands.SetRoomArchived;
using MedApp.Application.Sites.Commands.SetSiteArchived;
using MedApp.Application.Sites.Commands.UpdateRoom;
using MedApp.Application.Sites.Commands.UpdateSite;
using MedApp.Application.Sites.Queries.GetRooms;
using MedApp.Application.Sites.Queries.GetSiteById;
using MedApp.Application.Sites.Queries.GetSites;
using MedApp.Contracts.Sites.Requests;
using MedApp.Contracts.Sites.Responses;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.AspNetCore.Mvc;
using static MedApp.UnitTests.Common.Constants.ApiTestConstants;
using static MedApp.UnitTests.Common.Constants.SitesTestConstants;

namespace MedApp.UnitTests.Api.Controllers;

[TestFixture]
public sealed class SitesControllerTests
{
    /// <summary>Sites and rooms are organisation-wide setup, so only admins change them.</summary>
    private static readonly string[] AdminOnly =
    [
        nameof(SitesController.CreateSite),
        nameof(SitesController.UpdateSite),
        nameof(SitesController.ArchiveSite),
        nameof(SitesController.RestoreSite),
        nameof(SitesController.DeleteSite),
        nameof(SitesController.SetSiteHours),
        nameof(SitesController.CreateRoom),
        nameof(SitesController.UpdateRoom),
        nameof(SitesController.ArchiveRoom),
        nameof(SitesController.RestoreRoom),
        nameof(SitesController.DeleteRoom),
    ];

    private IFixture _fixture = null!;
    private CancellationTokenSource _cts = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();
        _cts = new CancellationTokenSource();
    }

    [TearDown]
    public void TearDown() => _cts.Dispose();

    private static SitesController Controller(RecordingMediator mediator) =>
        new SitesController(mediator).WithRequest(SignedIn("Admin"));

    private async Task ExpectOk(object response, object request, Func<SitesController, Task<IActionResult>> act)
    {
        var mediator = new RecordingMediator(_ => response);

        var result = await act(Controller(mediator));

        mediator.Request.Should().Be(request);
        mediator.Tokens.Single().Should().Be(_cts.Token);
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(response);
    }

    private async Task ExpectNoContent(object request, Func<SitesController, Task<IActionResult>> act)
    {
        var mediator = new RecordingMediator();

        var result = await act(Controller(mediator));

        mediator.Request.Should().Be(request);
        mediator.Tokens.Single().Should().Be(_cts.Token);
        result.Should().BeOfType<NoContentResult>();
    }

    private static SiteRequest Request() => new()
    {
        Name = SiteName,
        AddressLines = [AddressLine1, AddressLine2],
        Postcode = Postcode,
        Phone = Phone
    };

    // Hours

    [Test]
    public async Task GetSiteHours_ReturnsThem() =>
        await ExpectOk(_fixture.Create<SiteHoursResponse>(), new GetSiteHoursQuery(SiteId), c => c.GetSiteHours(SiteId, _cts.Token));

    [Test]
    public async Task SetSiteHours_SendsEachDay()
    {
        var response = _fixture.Create<SiteHoursResponse>();
        var mediator = new RecordingMediator(_ => response);
        var day = new SiteHoursDayRequest { DayOfWeek = "Tuesday", Opens = new TimeOnly(8, 0), Closes = new TimeOnly(20, 0) };

        var result = await Controller(mediator).SetSiteHours(SiteId, new SiteHoursRequest { Days = [day] }, _cts.Token);

        mediator.Request.Should().BeEquivalentTo(
            new SetSiteHoursCommand(SiteId, [new SiteHoursDay("Tuesday", new TimeOnly(8, 0), new TimeOnly(20, 0))]));
        mediator.Tokens.Single().Should().Be(_cts.Token);
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(response);
    }

    // Sites

    [TestCase(true)]
    [TestCase(false)]
    public async Task GetSites_AsksForArchivedOnesOrNot(bool includeArchived)
    {
        IReadOnlyList<SiteResponse> sites = [];
        await ExpectOk(sites, new GetSitesQuery(includeArchived), c => c.GetSites(includeArchived, _cts.Token));
    }

    [Test]
    public async Task GetSite_ReturnsIt() =>
        await ExpectOk(_fixture.Create<SiteResponse>(), new GetSiteByIdQuery(RecordId), c => c.GetSite(RecordId, _cts.Token));

    [Test]
    public async Task CreateSite_SendsTheDetailsAndReturnsItWithItsAddress()
    {
        var request = Request();
        var site = _fixture.Create<SiteResponse>();
        var mediator = new RecordingMediator(_ => site);

        var result = await Controller(mediator).CreateSite(request, _cts.Token);

        var command = mediator.Request.Should().BeOfType<CreateSiteCommand>().Subject;
        command.Name.Should().Be(SiteName);
        command.AddressLines.Should().Equal(AddressLine1, AddressLine2);
        command.Postcode.Should().Be(Postcode);
        command.Phone.Should().Be(Phone);
        mediator.Tokens.Single().Should().Be(_cts.Token);
        var created = result.Should().BeOfType<CreatedResult>().Subject;
        created.Location.Should().Be($"/api/sites/{site.Id}");
        created.Value.Should().Be(site);
    }

    [Test]
    public async Task UpdateSite_SendsTheDetails()
    {
        var request = Request();
        var site = _fixture.Create<SiteResponse>();
        var mediator = new RecordingMediator(_ => site);

        var result = await Controller(mediator).UpdateSite(RecordId, request, _cts.Token);

        var command = mediator.Request.Should().BeOfType<UpdateSiteCommand>().Subject;
        command.SiteId.Should().Be(RecordId);
        command.Name.Should().Be(SiteName);
        command.AddressLines.Should().Equal(AddressLine1, AddressLine2);
        command.Postcode.Should().Be(Postcode);
        command.Phone.Should().Be(Phone);
        mediator.Tokens.Single().Should().Be(_cts.Token);
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(site);
    }

    [Test]
    public async Task ArchiveAndRestoreSite_SetItsArchivedFlag()
    {
        var site = _fixture.Create<SiteResponse>();
        await ExpectOk(site, new SetSiteArchivedCommand(RecordId, true), c => c.ArchiveSite(RecordId, _cts.Token));
        await ExpectOk(site, new SetSiteArchivedCommand(RecordId, false), c => c.RestoreSite(RecordId, _cts.Token));
    }

    [Test]
    public async Task DeleteSite_ReturnsNoContent() =>
        await ExpectNoContent(new DeleteSiteCommand(RecordId), c => c.DeleteSite(RecordId, _cts.Token));

    // Rooms

    [TestCase(true)]
    [TestCase(false)]
    public async Task GetRooms_AsksForASitesArchivedOnesOrNot(bool includeArchived)
    {
        IReadOnlyList<RoomResponse> rooms = [];
        await ExpectOk(rooms, new GetRoomsQuery(OtherId, includeArchived), c => c.GetRooms(OtherId, includeArchived, _cts.Token));
    }

    [Test]
    public async Task GetRooms_NoSite_AsksForEverySites()
    {
        IReadOnlyList<RoomResponse> rooms = [];
        await ExpectOk(rooms, new GetRoomsQuery(null, false), c => c.GetRooms(null, false, _cts.Token));
    }

    [Test]
    public async Task CreateRoom_SendsTheSiteAndNameAndReturnsItWithItsAddress()
    {
        var room = _fixture.Create<RoomResponse>();
        var mediator = new RecordingMediator(_ => room);

        var result = await Controller(mediator).CreateRoom(new CreateRoomRequest { SiteId = OtherId, Name = RoomName }, _cts.Token);

        mediator.Request.Should().Be(new CreateRoomCommand(OtherId, RoomName));
        mediator.Tokens.Single().Should().Be(_cts.Token);
        var created = result.Should().BeOfType<CreatedResult>().Subject;
        created.Location.Should().Be($"/api/rooms?siteId={room.SiteId}");
        created.Value.Should().Be(room);
    }

    [Test]
    public async Task UpdateRoom_SendsTheName() =>
        await ExpectOk(_fixture.Create<RoomResponse>(),
            new UpdateRoomCommand(RecordId, OtherRoomName),
            c => c.UpdateRoom(RecordId, new UpdateRoomRequest { Name = OtherRoomName }, _cts.Token));

    [Test]
    public async Task ArchiveAndRestoreRoom_SetItsArchivedFlag()
    {
        var room = _fixture.Create<RoomResponse>();
        await ExpectOk(room, new SetRoomArchivedCommand(RecordId, true), c => c.ArchiveRoom(RecordId, _cts.Token));
        await ExpectOk(room, new SetRoomArchivedCommand(RecordId, false), c => c.RestoreRoom(RecordId, _cts.Token));
    }

    [Test]
    public async Task DeleteRoom_ReturnsNoContent() =>
        await ExpectNoContent(new DeleteRoomCommand(RecordId), c => c.DeleteRoom(RecordId, _cts.Token));

    // Who

    [Test]
    public async Task Changes_AdminsOnly()
    {
        foreach (var action in AdminOnly)
        {
            (await ControllerAuthorisation.MayCallAsync<SitesController>(action, SignedIn("User"))).Should().BeFalse(action);
            (await ControllerAuthorisation.MayCallAsync<SitesController>(action, SignedIn("Admin"))).Should().BeTrue(action);
            (await ControllerAuthorisation.MayCallAsync<SitesController>(action, SignedIn())).Should().BeFalse(action);
        }
    }

    [Test]
    public async Task Reading_AnyStaff()
    {
        var anyStaff = ControllerAuthorisation.Actions<SitesController>().Except(AdminOnly).ToList();
        anyStaff.Should().BeEquivalentTo(nameof(SitesController.GetSites), nameof(SitesController.GetSite), nameof(SitesController.GetRooms),
            nameof(SitesController.GetSiteHours));

        foreach (var action in anyStaff)
        {
            (await ControllerAuthorisation.MayCallAsync<SitesController>(action, SignedIn("User"))).Should().BeTrue(action);
            (await ControllerAuthorisation.MayCallAsync<SitesController>(action, SignedIn("Admin"))).Should().BeTrue(action);
            (await ControllerAuthorisation.MayCallAsync<SitesController>(action, SignedIn())).Should().BeFalse(action);
        }
    }
}
