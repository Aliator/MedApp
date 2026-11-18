using FluentAssertions;
using MedApp.Application.Sites.Commands.UpdateSite;
using static MedApp.UnitTests.Common.Constants.SitesTestConstants;

namespace MedApp.UnitTests.Application.Sites.Commands.UpdateSite;

[TestFixture]
public sealed class UpdateSiteValidatorTests
{
    private readonly UpdateSiteValidator _validator = new();

    private static UpdateSiteCommand Valid() => new(SiteId, SiteName, [AddressLine1], Postcode, Phone);

    [Test]
    public void Validate_FullDetails_IsValid() => _validator.Validate(Valid()).IsValid.Should().BeTrue();

    [Test]
    public void Validate_NoSiteId_IsInvalid() =>
        _validator.Validate(Valid() with { SiteId = Guid.Empty }).Errors.Should().ContainSingle(e => e.PropertyName == "SiteId");

    [Test]
    public void Validate_NoName_IsInvalid() =>
        _validator.Validate(Valid() with { Name = " " }).Errors.Should().ContainSingle(e => e.PropertyName == "Name");

    [Test]
    public void Validate_BadAddressPostcodeAndPhone_NamesEach() =>
        _validator.Validate(Valid() with { AddressLines = ["a", "b", "c", "d"], Postcode = "ZZ", Phone = "none" })
            .Errors.Select(e => e.PropertyName).Should().BeEquivalentTo("AddressLines", "Postcode", "Phone");
}
