using FluentAssertions;
using MedApp.Application.Auth.Commands.Login;

namespace MedApp.UnitTests.Application.Auth.Commands.Login;

[TestFixture]
public sealed class LoginValidatorTests
{
    private LoginValidator _validator = null!;

    [SetUp]
    public void SetUp()
    {
        _validator = new LoginValidator();
    }

    [Test]
    public void Validate_ValidCommand_Passes()
    {
        var command = new LoginCommand(
            "user",
            "password");

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_EmptyUsername_Fails()
    {
        var command = new LoginCommand(
            string.Empty,
            "password");

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Username");
    }

    [Test]
    public void Validate_EmptyPassword_Fails()
    {
        var command = new LoginCommand(
            "user",
            string.Empty);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Password");
    }
}