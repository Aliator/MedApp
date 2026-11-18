using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Sites.Commands.UpdateSite;
using MedApp.Domain.Sites;
using Moq;
using static MedApp.UnitTests.Common.Constants.SitesTestConstants;

namespace MedApp.UnitTests.Application.Sites.Commands.UpdateSite;

[TestFixture]
public sealed class UpdateSiteHandlerTests : SiteHandlerTestBase
{
    private UpdateSiteHandler Handler => Fixture.Create<UpdateSiteHandler>();

    [Test]
    public async Task Handle_NewDetails_ReplacesThemAndKeepsTheRooms()
    {
        var site = NewSite();
        GivenSites(site);
        GivenRooms(NewRoom());

        var response = await Handler.Handle(new UpdateSiteCommand(SiteId, $" {OtherSiteName} ", [AddressLine2], "a11aa", null), Token);

        site.Name.Should().Be(OtherSiteName);
        site.AddressLines.Should().Be(AddressLine2);
        site.Postcode.Should().Be("A1 1AA");
        site.Phone.Should().BeNull();
        site.LastUpdated.Should().Be(Now);
        site.CreatedAt.Should().Be(Earlier);
        Repository.Verify(r => r.UpdateSiteAsync(site, Token), Times.Once);

        response.Name.Should().Be(OtherSiteName);
        response.Rooms.Should().ContainSingle().Which.SiteName.Should().Be(OtherSiteName);
        response.CanBeDeleted.Should().BeFalse();
    }

    [Test]
    public async Task Handle_SameName_DoesNotCheckIt()
    {
        GivenSites(NewSite());

        await Handler.Handle(new UpdateSiteCommand(SiteId, SiteName, null, null, null), Token);

        Repository.Verify(r => r.SiteNameExistsAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_AnotherSitesName_ThrowsConflict()
    {
        GivenSites(NewSite(), NewSite(OtherSiteId, OtherSiteName));

        var act = () => Handler.Handle(new UpdateSiteCommand(SiteId, OtherSiteName, null, null, null), Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage($"A site called '{OtherSiteName}' already exists.");
        Repository.Verify(r => r.UpdateSiteAsync(It.IsAny<Site>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_UnknownSite_ThrowsNotFound()
    {
        var act = () => Handler.Handle(new UpdateSiteCommand(SiteId, SiteName, null, null, null), Token);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
