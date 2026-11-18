using FluentAssertions;
using MedApp.Application.Sites.Commands.CreateSite;
using static MedApp.UnitTests.Common.Constants.SitesTestConstants;

namespace MedApp.UnitTests.Application.Sites.Commands.CreateSite;

[TestFixture]
public sealed class CreateSiteValidatorTests
{
    private readonly CreateSiteValidator _validator = new();

    private static CreateSiteCommand Valid() => new(SiteName, [AddressLine1, AddressLine2], Postcode, Phone);

    [Test]
    public void Validate_FullDetails_IsValid() =>
        _validator.Validate(Valid()).IsValid.Should().BeTrue();

    [Test]
    public void Validate_NameOnly_IsValid() =>
        _validator.Validate(new CreateSiteCommand(SiteName, null, null, null)).IsValid.Should().BeTrue();

    [TestCase("")]
    [TestCase("   ")]
    public void Validate_NoName_IsInvalid(string name) =>
        _validator.Validate(Valid() with { Name = name }).Errors.Should().ContainSingle(e => e.PropertyName == "Name");

    [Test]
    public void Validate_NameTooLong_IsInvalid() =>
        _validator.Validate(Valid() with { Name = new string('a', 101) }).Errors.Should().ContainSingle(e => e.PropertyName == "Name");

    [Test]
    public void Validate_FourAddressLines_IsInvalid() =>
        _validator.Validate(Valid() with { AddressLines = ["a", "b", "c", "d"] })
            .Errors.Should().ContainSingle(e => e.PropertyName == "AddressLines");

    [Test]
    public void Validate_ThreeLinesAndBlanks_IsValid() =>
        _validator.Validate(Valid() with { AddressLines = ["a", "", "b", " ", "c", null] }).IsValid.Should().BeTrue();

    [Test]
    public void Validate_AddressLineTooLong_IsInvalid() =>
        _validator.Validate(Valid() with { AddressLines = [new string('a', 101)] })
            .Errors.Should().ContainSingle(e => e.PropertyName == "AddressLines");

    [Test]
    public void Validate_AddressLineWithALineBreak_IsInvalid() =>
        _validator.Validate(Valid() with { AddressLines = ["1 Example Street\nTestville"] })
            .Errors.Should().ContainSingle(e => e.PropertyName == "AddressLines");

    [TestCase("ZZ99 3AA")]
    [TestCase("zz993aa")]
    [TestCase("A1 1AA")]
    [TestCase("AB1C 2DE")]
    [TestCase("")]
    [TestCase(null)]
    public void Validate_PostcodeInFormatOrNone_IsValid(string? postcode) =>
        _validator.Validate(Valid() with { Postcode = postcode }).IsValid.Should().BeTrue();

    [TestCase("ZZ99")]
    [TestCase("99ZZ 3AA")]
    [TestCase("ZZ99 3A")]
    [TestCase("not a postcode")]
    public void Validate_PostcodeNotInFormat_IsInvalid(string postcode) =>
        _validator.Validate(Valid() with { Postcode = postcode }).Errors.Should().ContainSingle(e => e.PropertyName == "Postcode");

    [TestCase("07700 900123")]
    [TestCase("+44 (0)7700 900123")]
    [TestCase("0115-496-0000")]
    [TestCase("")]
    [TestCase(null)]
    public void Validate_PhoneOrNone_IsValid(string? phone) =>
        _validator.Validate(Valid() with { Phone = phone }).IsValid.Should().BeTrue();

    [TestCase("call reception")]
    [TestCase("12345")]
    [TestCase("0770090012301234567890123456789")]
    public void Validate_NotAPhone_IsInvalid(string phone) =>
        _validator.Validate(Valid() with { Phone = phone }).Errors.Should().ContainSingle(e => e.PropertyName == "Phone");
}
