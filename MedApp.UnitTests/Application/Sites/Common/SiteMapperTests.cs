using FluentAssertions;
using MedApp.Application.Sites.Common;
using static MedApp.UnitTests.Common.Constants.SitesTestConstants;

namespace MedApp.UnitTests.Application.Sites.Common;

[TestFixture]
public sealed class SiteMapperTests
{
    [Test]
    public void ToResponse_Site_CarriesItsDetailsAndOnlyItsOwnRoomsByName()
    {
        var site = NewSite();
        var rooms = new[]
        {
            NewRoom(OtherRoomId, name: "Treatment room"),
            NewRoom(),
            NewRoom(Guid.NewGuid(), OtherSiteId, "Elsewhere"),
        };

        var response = SiteMapper.ToResponse(site, rooms, inUse: false, isLastOpen: false, roomsInUse: new HashSet<Guid> { RoomId });

        response.Id.Should().Be(SiteId);
        response.Name.Should().Be(SiteName);
        response.AddressLines.Should().Equal(AddressLine1, AddressLine2);
        response.Postcode.Should().Be(Postcode);
        response.Phone.Should().Be(Phone);
        response.IsArchived.Should().BeFalse();
        response.CanBeDeleted.Should().BeTrue();
        response.CreatedAt.Should().Be(Earlier);
        response.LastUpdated.Should().Be(Earlier);
        response.Rooms.Select(r => (r.Name, r.SiteName, r.CanBeDeleted)).Should().Equal(
            (RoomName, SiteName, false), ("Treatment room", SiteName, true));
    }

    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public void ToResponse_UsedOrTheLastOpenSite_CantBeDeleted(bool inUse, bool isLastOpen) =>
        SiteMapper.ToResponse(NewSite(), [], inUse, isLastOpen, new HashSet<Guid>()).CanBeDeleted.Should().BeFalse();

    [Test]
    public void ToResponse_NoAddress_HasNoLines()
    {
        var site = NewSite();
        site.AddressLines = null;

        SiteMapper.ToResponse(site, [], false, false, new HashSet<Guid>()).AddressLines.Should().BeEmpty();
    }

    [Test]
    public void ToResponse_Room_NamesItsSite()
    {
        var response = SiteMapper.ToResponse(NewRoom(isArchived: true), NewSite(), inUse: true);

        response.Id.Should().Be(RoomId);
        response.SiteId.Should().Be(SiteId);
        response.SiteName.Should().Be(SiteName);
        response.Name.Should().Be(RoomName);
        response.IsArchived.Should().BeTrue();
        response.CanBeDeleted.Should().BeFalse();
    }

    [Test]
    public void JoinAndSplitAddress_RoundTripTheLines()
    {
        SiteRules.JoinAddress([" a ", null, "", "b"]).Should().Be("a\nb");
        SiteRules.JoinAddress([]).Should().BeNull();
        SiteRules.JoinAddress(null).Should().BeNull();
        SiteRules.SplitAddress("a\nb").Should().Equal("a", "b");
        SiteRules.SplitAddress(null).Should().BeEmpty();
    }
}
