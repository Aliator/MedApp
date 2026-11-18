using FluentAssertions;
using MedApp.Application.Common.Contacts;
using MedApp.Application.Practices.Commands.CreatePractice;
using MedApp.Application.Practices.Commands.UpdatePractice;
using MedApp.Application.Practices.Common;
using MedApp.Domain.Practices;
using Microsoft.Extensions.Options;
using static MedApp.UnitTests.Common.Constants.PracticesTestConstants;

namespace MedApp.UnitTests.Application.Practices.Commands;

[TestFixture]
public sealed class PracticeValidatorsTests
{
    private static CreatePracticeValidator Create(params string[] domains) =>
        new(Options.Create(new LettersOptions { SecureEmailDomains = domains.Length == 0 ? null : domains }));

    private static CreatePracticeCommand Command(
        string name = PracticeName,
        string? odsCode = OdsCode,
        IReadOnlyList<string>? lines = null,
        string? postcode = Postcode,
        string? phone = Phone,
        string? email = SecureEmail) =>
        new(name, odsCode, lines ?? [AddressLine1, AddressLine2], postcode, phone, email);

    [Test]
    public void Create_Valid_Passes() => Create().Validate(Command()).IsValid.Should().BeTrue();

    [Test]
    public void Create_OnlyAName_Passes() =>
        Create().Validate(new CreatePracticeCommand(PracticeName, null, null, null, null, null)).IsValid.Should().BeTrue();

    [TestCase("")]
    [TestCase("   ")]
    public void Create_NoName_Fails(string name) =>
        Create().Validate(Command(name: name)).Errors.Should().ContainSingle(e => e.PropertyName == "Name");

    [Test]
    public void Create_NameTooLong_Fails() =>
        Create().Validate(Command(name: new string('a', Practice.MaxNameLength + 1)))
            .Errors.Should().ContainSingle(e => e.PropertyName == "Name");

    [TestCase("Z9")]
    [TestCase("Z99-901")]
    [TestCase("Z999999999999")]
    public void Create_BadOdsCode_Fails(string code) =>
        Create().Validate(Command(odsCode: code)).Errors.Should().ContainSingle(e => e.PropertyName == "OdsCode");

    [Test]
    public void Create_FourAddressLines_Fails() =>
        Create().Validate(Command(lines: ["a", "b", "c", "d"]))
            .Errors.Should().ContainSingle(e => e.PropertyName == "AddressLines" && e.ErrorMessage == ContactRules.TooManyLines);

    [Test]
    public void Create_LongAddressLine_Fails() =>
        Create().Validate(Command(lines: [new string('a', 101)]))
            .Errors.Should().ContainSingle(e => e.PropertyName == "AddressLines" && e.ErrorMessage == ContactRules.LineTooLong);

    [Test]
    public void Create_BadPostcode_Fails() =>
        Create().Validate(Command(postcode: "ZZ1"))
            .Errors.Should().ContainSingle(e => e.PropertyName == "Postcode" && e.ErrorMessage == ContactRules.NotAPostcode);

    [TestCase("12345")]
    [TestCase("01632 960000 01632 960000 01632")]
    public void Create_BadPhone_Fails(string phone) =>
        Create().Validate(Command(phone: phone)).Errors.Should().ContainSingle(e => e.PropertyName == "Phone");

    [Test]
    public void Create_NotAnEmail_Fails() =>
        Create().Validate(Command(email: "not an address"))
            .Errors.Should().ContainSingle(e => e.PropertyName == "SecureEmail" && e.ErrorMessage.Contains("one email address"));

    [Test]
    public void Create_EmailOutsideTheAllowedDomains_Fails() =>
        Create().Validate(Command(email: "practice@example.org"))
            .Errors.Should().ContainSingle(e => e.PropertyName == "SecureEmail"
                                                && e.ErrorMessage == "A secure email address must be in nhs.net.");

    [Test]
    public void Create_ConfiguredDomains_AreUsed()
    {
        var validator = Create("Secure.Example", "@nhs.net");

        validator.Validate(Command(email: "team@sub.secure.example")).IsValid.Should().BeTrue();
        validator.Validate(Command(email: "team@other.example"))
            .Errors.Should().ContainSingle(e => e.ErrorMessage == "A secure email address must be in secure.example or nhs.net.");
    }

    private static UpdatePracticeValidator Update() => new(Options.Create(new LettersOptions()));

    [Test]
    public void Update_Valid_Passes() =>
        Update().Validate(new UpdatePracticeCommand(Guid.NewGuid(), PracticeName, OdsCode, null, Postcode, Phone, SecureEmail, 2))
            .IsValid.Should().BeTrue();

    [Test]
    public void Update_NoIdOrVersion_Fails()
    {
        var result = Update().Validate(new UpdatePracticeCommand(Guid.Empty, PracticeName, null, null, null, null, null, 0));

        result.Errors.Select(e => e.PropertyName).Should().BeEquivalentTo(["PracticeId", "ExpectedVersion"]);
    }

    [Test]
    public void Update_ChecksTheDetailsAsAddingDoes() =>
        Update().Validate(new UpdatePracticeCommand(Guid.NewGuid(), " ", "!", null, "nope", "nope", "nope", 1))
            .Errors.Select(e => e.PropertyName).Should().BeEquivalentTo(["Name", "OdsCode", "Postcode", "Phone", "SecureEmail"]);
}
