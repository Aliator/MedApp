using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MedApp.Application.Sites.Common;
using MedApp.Contracts.Sites.Requests;
using MedApp.Contracts.Sites.Responses;
using MedApp.Domain.Sites;
using MedApp.IntegrationTests.Common;
using Microsoft.AspNetCore.Mvc;
using static MedApp.IntegrationTests.Common.Constants.IntegrationTestConstants;

namespace MedApp.IntegrationTests.Sites;

/// <summary>
/// Sites and rooms: every database has "Main site"; admins add, edit, archive and delete them, staff read them; names
/// are unique (rooms within a site, by unique indexes too); there's always an open site; and a used one is archived,
/// not deleted.
/// </summary>
[TestFixture]
public sealed class SiteTests : IntegrationTest
{
    private HttpClient _admin = null!;
    private StaffAccount _staff = null!;

    [SetUp]
    public async Task SetUpAsync()
    {
        _admin = await SignInAsAdminAsync();
        _staff = await CreateStaffAsync(UserRole);
    }

    private static async Task<List<SiteResponse>> ListAsync(HttpClient client, bool includeArchived = false) =>
        await (await client.GetAsync($"/api/sites?includeArchived={includeArchived}")).ReadAsAsync<List<SiteResponse>>();

    [Test]
    public async Task List_AFreshDatabase_HasTheMainSite_WhichCantBeDeleted()
    {
        var site = (await ListAsync(_staff.Client)).Should().ContainSingle().Subject;

        site.Id.Should().Be(Site.MainSiteId);
        site.Name.Should().Be(Site.MainSiteName);
        site.CanBeDeleted.Should().BeFalse("it's the only open site");
        await (await _admin.DeleteAsync($"/api/sites/{Site.MainSiteId}")).ShouldHaveStatusAsync(HttpStatusCode.Conflict);
        await (await _admin.PostAsync($"/api/sites/{Site.MainSiteId}/archive", null)).ShouldHaveStatusAsync(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task AddAndEdit_AsAdmin_StoresTheTidiedDetails_AndStaffSeeThem()
    {
        var response = await _admin.PostAsJsonAsync("/api/sites", new SiteRequest
        {
            Name = "Northgate outreach clinic",
            AddressLines = ["1 Example Street", "", "Testville"],
            Postcode = "zz993aa",
            Phone = "07700 900123"
        });
        var added = await response.ReadAsAsync<SiteResponse>(HttpStatusCode.Created);
        response.Headers.Location!.ToString().Should().Be($"/api/sites/{added.Id}");
        added.AddressLines.Should().Equal("1 Example Street", "Testville");
        added.Postcode.Should().Be("ZZ99 3AA");

        var edited = await (await _admin.PutAsJsonAsync($"/api/sites/{added.Id}", new SiteRequest { Name = "Riverside centre" }))
            .ReadAsAsync<SiteResponse>();
        edited.Name.Should().Be("Riverside centre");
        edited.AddressLines.Should().BeEmpty();
        edited.Postcode.Should().BeNull();
        edited.Phone.Should().BeNull();

        var seen = await (await _staff.Client.GetAsync($"/api/sites/{added.Id}")).ReadAsAsync<SiteResponse>();
        seen.Name.Should().Be("Riverside centre");
        (await ListAsync(_staff.Client)).Select(s => s.Name).Should().Equal(Site.MainSiteName, "Riverside centre");
    }

    [Test]
    public async Task Writes_AsStaff_Return403()
    {
        await (await PostSiteAsync(_staff.Client)).ShouldHaveStatusAsync(HttpStatusCode.Forbidden);
        await (await _staff.Client.PutAsJsonAsync($"/api/sites/{Site.MainSiteId}", new SiteRequest { Name = "Mine" }))
            .ShouldHaveStatusAsync(HttpStatusCode.Forbidden);
        await (await _staff.Client.PostAsJsonAsync("/api/rooms", new CreateRoomRequest { SiteId = Site.MainSiteId, Name = "Room 1" }))
            .ShouldHaveStatusAsync(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Add_BadDetails_Returns400NamingThem()
    {
        var response = await _admin.PostAsJsonAsync("/api/sites", new SiteRequest
        {
            Name = "",
            AddressLines = ["a", "b", "c", "d"],
            Postcode = "nowhere",
            Phone = "call us"
        });

        var problem = await response.ReadAsAsync<ValidationProblemDetails>(HttpStatusCode.BadRequest);
        problem.Errors.Keys.Should().BeEquivalentTo("Name", "AddressLines", "Postcode", "Phone");
    }

    [Test]
    public async Task Add_ANameTakenInAnyCase_Returns409()
    {
        await CreateSiteAsync(_admin, "Riverside centre");

        await (await PostSiteAsync(_admin, "RIVERSIDE CENTRE")).ShouldHaveStatusAsync(HttpStatusCode.Conflict);
        await (await PostSiteAsync(_admin, Site.MainSiteName)).ShouldHaveStatusAsync(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task Rooms_AddRenameArchiveRestoreAndDelete()
    {
        var site = await CreateSiteAsync(_admin);
        var room = await CreateRoomAsync(_admin, site.Id, "Room 2");
        room.SiteName.Should().Be(site.Name);
        room.CanBeDeleted.Should().BeTrue();

        // Names are unique within a site, not across sites.
        await (await _admin.PostAsJsonAsync("/api/rooms", new CreateRoomRequest { SiteId = site.Id, Name = "room 2" }))
            .ShouldHaveStatusAsync(HttpStatusCode.Conflict);
        await CreateRoomAsync(_admin, Site.MainSiteId, "Room 2");

        var renamed = await (await _admin.PutAsJsonAsync($"/api/rooms/{room.Id}", new UpdateRoomRequest { Name = "Treatment room" }))
            .ReadAsAsync<RoomResponse>();
        renamed.Name.Should().Be("Treatment room");

        (await (await _admin.PostAsync($"/api/rooms/{room.Id}/archive", null)).ReadAsAsync<RoomResponse>()).IsArchived.Should().BeTrue();
        (await (await _staff.Client.GetAsync($"/api/rooms?siteId={site.Id}")).ReadAsAsync<List<RoomResponse>>()).Should().BeEmpty();
        (await (await _staff.Client.GetAsync($"/api/rooms?siteId={site.Id}&includeArchived=true")).ReadAsAsync<List<RoomResponse>>())
            .Should().ContainSingle().Which.Name.Should().Be("Treatment room");
        (await (await _admin.PostAsync($"/api/rooms/{room.Id}/restore", null)).ReadAsAsync<RoomResponse>()).IsArchived.Should().BeFalse();

        await (await _admin.DeleteAsync($"/api/rooms/{room.Id}")).ShouldHaveStatusAsync(HttpStatusCode.NoContent);
        await (await _staff.Client.GetAsync($"/api/rooms?siteId={Guid.NewGuid()}")).ShouldHaveStatusAsync(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task ArchiveSite_WithOpenRooms_Returns409_ThenArchivesOnceTheyAre_AndItsRoomsCantComeBack()
    {
        var site = await CreateSiteAsync(_admin);
        var room = await CreateRoomAsync(_admin, site.Id);

        var refused = await (await _admin.PostAsync($"/api/sites/{site.Id}/archive", null))
            .ReadAsAsync<ProblemDetails>(HttpStatusCode.Conflict);
        refused.Title.Should().Contain(room.Name);

        await _admin.PostAsync($"/api/rooms/{room.Id}/archive", null);
        var archived = await (await _admin.PostAsync($"/api/sites/{site.Id}/archive", null)).ReadAsAsync<SiteResponse>();
        archived.IsArchived.Should().BeTrue();
        archived.CanBeDeleted.Should().BeFalse("it has a room");

        (await ListAsync(_staff.Client)).Should().NotContain(s => s.Id == site.Id);
        (await ListAsync(_staff.Client, includeArchived: true)).Should().Contain(s => s.Id == site.Id);
        await (await _admin.PostAsync($"/api/rooms/{room.Id}/restore", null)).ShouldHaveStatusAsync(HttpStatusCode.Conflict);
        await (await _admin.PostAsJsonAsync("/api/rooms", new CreateRoomRequest { SiteId = site.Id, Name = "New room" }))
            .ShouldHaveStatusAsync(HttpStatusCode.Conflict);
        await (await _admin.DeleteAsync($"/api/sites/{site.Id}")).ShouldHaveStatusAsync(HttpStatusCode.Conflict);

        (await (await _admin.PostAsync($"/api/sites/{site.Id}/restore", null)).ReadAsAsync<SiteResponse>()).IsArchived.Should().BeFalse();
    }

    [Test]
    public async Task DeleteSite_Unused_DeletesIt_WithAClosedDay_Returns409()
    {
        var unused = await CreateSiteAsync(_admin);
        unused.CanBeDeleted.Should().BeTrue();
        await (await _admin.DeleteAsync($"/api/sites/{unused.Id}")).ShouldHaveStatusAsync(HttpStatusCode.NoContent);
        await (await _admin.GetAsync($"/api/sites/{unused.Id}")).ShouldHaveStatusAsync(HttpStatusCode.NotFound);

        var closed = await CreateSiteAsync(_admin);
        await AddClosedDayAsync(_admin, NextWorkingDay(2), siteId: closed.Id);

        var site = await (await _admin.GetAsync($"/api/sites/{closed.Id}")).ReadAsAsync<SiteResponse>();
        site.CanBeDeleted.Should().BeFalse();
        var refused = await (await _admin.DeleteAsync($"/api/sites/{closed.Id}")).ReadAsAsync<ProblemDetails>(HttpStatusCode.Conflict);
        refused.Title.Should().Be(SiteRules.SiteInUse(new Site { Name = closed.Name }));
    }
}
