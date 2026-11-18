using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Sites.Commands.CreateSite;
using MedApp.Domain.Sites;
using Moq;
using static MedApp.UnitTests.Common.Constants.SitesTestConstants;

namespace MedApp.UnitTests.Application.Sites.Commands.CreateSite;

[TestFixture]
public sealed class CreateSiteHandlerTests : SiteHandlerTestBase
{
    private CreateSiteHandler Handler => Fixture.Create<CreateSiteHandler>();

    [Test]
    public async Task Handle_NewName_AddsTheSiteWithItsDetailsTidied()
    {
        GivenSites(NewSite(OtherSiteId, OtherSiteName));

        var response = await Handler.Handle(
            new CreateSiteCommand($"  {SiteName} ", [$" {AddressLine1} ", "", null, AddressLine2], " zz993aa ", $" {Phone} "), Token);

        var site = Sites.Should().ContainSingle(s => s.Name == SiteName).Subject;
        site.Id.Should().NotBeEmpty();
        site.AddressLines.Should().Be($"{AddressLine1}\n{AddressLine2}");
        site.Postcode.Should().Be(Postcode);
        site.Phone.Should().Be(Phone);
        site.IsArchived.Should().BeFalse();
        site.CreatedAt.Should().Be(Now);
        site.LastUpdated.Should().Be(Now);
        Repository.Verify(r => r.AddSiteAsync(site, Token), Times.Once);

        response.Id.Should().Be(site.Id);
        response.Name.Should().Be(SiteName);
        response.AddressLines.Should().Equal(AddressLine1, AddressLine2);
        response.Postcode.Should().Be(Postcode);
        response.Rooms.Should().BeEmpty();
        response.CanBeDeleted.Should().BeTrue();
    }

    [Test]
    public async Task Handle_NoAddressPostcodeOrPhone_StoresNone()
    {
        await Handler.Handle(new CreateSiteCommand(SiteName, null, " ", ""), Token);

        var site = Sites.Should().ContainSingle().Subject;
        site.AddressLines.Should().BeNull();
        site.Postcode.Should().BeNull();
        site.Phone.Should().BeNull();
    }

    [Test]
    public async Task Handle_NameTaken_ThrowsConflict()
    {
        GivenSites(NewSite());

        var act = () => Handler.Handle(new CreateSiteCommand(SiteName.ToUpperInvariant(), null, null, null), Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage($"A site called '{SiteName.ToUpperInvariant()}' already exists.");
        Repository.Verify(r => r.AddSiteAsync(It.IsAny<Site>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
