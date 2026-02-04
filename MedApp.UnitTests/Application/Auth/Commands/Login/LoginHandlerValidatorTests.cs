using FluentAssertions;
using MedApp.Application.Auth.Commands.Login;
using MedApp.UnitTests.Common.Constants;

namespace MedApp.UnitTests.Application.Auth.Commands.Login;

[TestFixture]
public sealed class LoginHandlerValidatorTests
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
            AuthTestConstants.Usernames[0],
            AuthTestConstants.Password,
            null,
            null);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_EmptyUsername_Fails()
    {
        var command = new LoginCommand(
            string.Empty,
            AuthTestConstants.Password,
            null,
            null);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Username");
    }

    [Test]
    public void Validate_EmptyPassword_Fails()
    {
        var command = new LoginCommand(
            AuthTestConstants.Usernames[0],
            string.Empty,
            null,
            null);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Password");
    }
}