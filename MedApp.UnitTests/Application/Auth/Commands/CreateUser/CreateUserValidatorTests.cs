using FluentAssertions;
using MedApp.Application.Auth.Commands.CreateUser;

namespace MedApp.UnitTests.Application.Auth.Commands.CreateUser;

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
            "username",
            "password");

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_EmptyUsername_Fails()
    {
        var command = new CreateUserCommand(
            string.Empty,
            "password");

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Username");
    }

    [Test]
    public void Validate_UsernameTooShort_Fails()
    {
        var command = new CreateUserCommand(
            "aa",
            "password");

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Username");
    }

    [Test]
    public void Validate_UsernameTooLong_Fails()
    {
        var command = new CreateUserCommand(
            new string('a', 51),
            "password");

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Username");
    }

    [Test]
    public void Validate_EmptyPassword_Fails()
    {
        var command = new CreateUserCommand(
            "username",
            string.Empty);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Password");
    }
}
