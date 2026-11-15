using FluentAssertions;
using MedApp.Application.Authentication.Users.Commands.CreateUser;
using MedApp.UnitTests.Common.Constants;

namespace MedApp.UnitTests.Application.Auth.Users.Commands.CreateUser;

[TestFixture]
public sealed class CreateUserValidatorTests
{
    private CreateUserValidator _validator = null!;

    [SetUp]
    public void SetUp()
    {
        _validator = new CreateUserValidator();
    }

    [Test]
    public void Validate_ValidCommand_Passes()
    {
        var command = new CreateUserCommand(
            AuthTestConstants.Usernames[0],
            AuthTestConstants.Passwords[0]);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_EmptyUsername_Fails()
    {
        var command = new CreateUserCommand(
            string.Empty,
            AuthTestConstants.Passwords[0]);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e 
            => e.PropertyName == nameof(CreateUserCommand.Username));
    }

    [Test]
    public void Validate_UsernameTooShort_Fails()
    {
        var command = new CreateUserCommand(
            "aa",
            AuthTestConstants.Passwords[0]);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e 
            => e.PropertyName == nameof(CreateUserCommand.Username));
    }

    [Test]
    public void Validate_UsernameTooLong_Fails()
    {
        var command = new CreateUserCommand(
            new string('a', 51),
            AuthTestConstants.Passwords[0]);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e 
            => e.PropertyName == nameof(CreateUserCommand.Username));
    }

    [Test]
    public void Validate_EmptyPassword_Fails()
    {
        var command = new CreateUserCommand(
            AuthTestConstants.Usernames[0],
            string.Empty);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e 
            => e.PropertyName == nameof(CreateUserCommand.Password));
    }
}
