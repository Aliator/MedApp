using FluentAssertions;
using MedApp.Application.Authentication.Settings.Commands.SaveMySettings;
using MedApp.UnitTests.Common.Constants;

namespace MedApp.UnitTests.Application.Auth.Settings.Commands.SaveMySettings;

[TestFixture]
public sealed class SaveMySettingsValidatorTests
{
    private readonly SaveMySettingsValidator _validator = new();

    private static SaveMySettingsCommand Command(string settings) => new(AuthTestConstants.SettingsUserId, settings);

    [Test]
    public void Validate_JsonObject_Passes()
    {
        _validator.Validate(Command(AuthTestConstants.SettingsJson)).IsValid.Should().BeTrue();
    }

    [TestCase("")]
    [TestCase("not json")]
    [TestCase("[1, 2]")]
    [TestCase("\"just a string\"")]
    public void Validate_NotAJsonObject_Fails(string settings)
    {
        _validator.Validate(Command(settings)).IsValid.Should().BeFalse();
    }

    [Test]
    public void Validate_OverTheLimit_Fails()
    {
        var tooLong = $"{{\"medapp.x\":\"{new string('a', SaveMySettingsValidator.MaxLength)}\"}}";

        _validator.Validate(Command(tooLong)).IsValid.Should().BeFalse();
    }
}
