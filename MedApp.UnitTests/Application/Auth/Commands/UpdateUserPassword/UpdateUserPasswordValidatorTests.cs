using FluentAssertions;
using MedApp.Application.Auth.Commands.UpdateUserPassword;
using MedApp.UnitTests.Common.Constants;

namespace MedApp.UnitTests.Application.Auth.Commands.UpdateUserPassword;

[TestFixture]
public sealed class UpdateUserValidatorTests
{
    private UpdateUserValidator _validator = null!;

    [SetUp]
    public void SetUp()
    {
        _validator = new UpdateUserValidator();
    }

    [Test]
    public void Validate_ValidCommand_Passes()
    {
        var command = new UpdateUserPasswordCommand(
            AuthTestConstants.Usernames[0],
            AuthTestConstants.Passwords[0],
            AuthTestConstants.Passwords[1]);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_EmptyUsername_Fails()
    {
        var command = new UpdateUserPasswordCommand(
            string.Empty,
            AuthTestConstants.Passwords[0],
            AuthTestConstants.Passwords[1]);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Username");
    }

    [Test]
    public void Validate_UsernameTooShort_Fails()
    {
        var command = new UpdateUserPasswordCommand(
            "aa",
            AuthTestConstants.Passwords[0],
            AuthTestConstants.Passwords[1]);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Username");
    }

    [Test]
    public void Validate_UsernameTooLong_Fails()
    {
        var command = new UpdateUserPasswordCommand(
            new string('a', 51),
            AuthTestConstants.Passwords[0],
            AuthTestConstants.Passwords[1]);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Username");
    }

    [Test]
    public void Validate_EmptyOldPassword_Fails()
    {
        var command = new UpdateUserPasswordCommand(
            AuthTestConstants.Usernames[0],
            string.Empty,
            AuthTestConstants.Passwords[1]);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "OldPassword");
    }

    [Test]
    public void Validate_EmptyNewPassword_Fails()
    {
        var command = new UpdateUserPasswordCommand(
            AuthTestConstants.Usernames[0],
            AuthTestConstants.Passwords[0],
            string.Empty);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "NewPassword");
    }
}
