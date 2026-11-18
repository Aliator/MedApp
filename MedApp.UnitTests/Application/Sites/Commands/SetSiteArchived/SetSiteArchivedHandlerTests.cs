using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Sites.Commands.SetSiteArchived;
using MedApp.Application.Sites.Common;
using MedApp.Domain.Sites;
using Moq;
using static MedApp.UnitTests.Common.Constants.SitesTestConstants;

namespace MedApp.UnitTests.Application.Sites.Commands.SetSiteArchived;

[TestFixture]
public sealed class SetSiteArchivedHandlerTests : SiteHandlerTestBase
{
    private SetSiteArchivedHandler Handler => Fixture.Create<SetSiteArchivedHandler>();

    [Test]
    public async Task Handle_ArchiveASiteWithOnlyArchivedRooms_ArchivesIt()
    {
        var site = NewSite();
        GivenSites(site, NewSite(OtherSiteId, OtherSiteName));
        GivenRooms(NewRoom(isArchived: true));

        var response = await Handler.Handle(new SetSiteArchivedCommand(SiteId, true), Token);

        site.IsArchived.Should().BeTrue();
        site.LastUpdated.Should().Be(Now);
        response.IsArchived.Should().BeTrue();
        response.Rooms.Should().ContainSingle();
        Repository.Verify(r => r.UpdateSiteAsync(site, Token), Times.Once);
    }

    [Test]
    public async Task Handle_Restore_RestoresIt()
    {
        var site = NewSite(isArchived: true);
        GivenSites(site, NewSite(OtherSiteId, OtherSiteName));

        var response = await Handler.Handle(new SetSiteArchivedCommand(SiteId, false), Token);

        site.IsArchived.Should().BeFalse();
        response.IsArchived.Should().BeFalse();
        Repository.Verify(r => r.UpdateSiteAsync(site, Token), Times.Once);
    }

    [Test]
    public async Task Handle_ArchiveTheLastOpenSite_ThrowsConflict()
    {
        var site = NewSite();
        GivenSites(site, NewSite(OtherSiteId, OtherSiteName, isArchived: true));

        var act = () => Handler.Handle(new SetSiteArchivedCommand(SiteId, true), Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(SiteRules.LastOpenSite);
        site.IsArchived.Should().BeFalse();
    }

    [Test]
    public async Task Handle_ArchiveASiteWithOpenRooms_ThrowsConflictNamingThem()
    {
        GivenSites(NewSite(), NewSite(OtherSiteId, OtherSiteName));
        GivenRooms(NewRoom(), NewRoom(OtherRoomId, name: OtherRoomName));

        var act = () => Handler.Handle(new SetSiteArchivedCommand(SiteId, true), Token);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage($"{SiteName} has rooms that aren't archived ({RoomName}, {OtherRoomName}). Archive them first.");
        Repository.Verify(r => r.UpdateSiteAsync(It.IsAny<Site>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task Handle_AlreadyAsAsked_SavesNothing(bool archived)
    {
        GivenSites(NewSite(isArchived: archived), NewSite(OtherSiteId, OtherSiteName));

        var response = await Handler.Handle(new SetSiteArchivedCommand(SiteId, archived), Token);

        response.IsArchived.Should().Be(archived);
        Repository.Verify(r => r.UpdateSiteAsync(It.IsAny<Site>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_UnknownSite_ThrowsNotFound()
    {
        var act = () => Handler.Handle(new SetSiteArchivedCommand(SiteId, true), Token);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task Handle_ArchiveASiteWhereStaffWork_ThrowsConflict()
    {
        var site = NewSite();
        GivenSites(site, NewSite(OtherSiteId, OtherSiteName));
        Hours.Staff.Add(MedApp.UnitTests.Common.Fixtures.InMemoryWorkingHours.StaffRow(
            Guid.NewGuid(), DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(17, 0), SiteId));

        var act = () => Handler.Handle(new SetSiteArchivedCommand(SiteId, true), Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(SiteRules.HasStaffHours(site));
        site.IsArchived.Should().BeFalse();
    }
}
