using FluentAssertions;
using MedApp.Application.Auth.Users.Commands.DeleteUser;
using MedApp.UnitTests.Common.Constants;

namespace MedApp.UnitTests.Application.Auth.Users.Commands.DeleteUser;

[TestFixture]
public sealed class DeleteUserValidatorTests
{
    private DeleteUserValidator _validator = null!;

    [SetUp]
    public void SetUp()
    {
        _validator = new DeleteUserValidator();
    }

    [Test]
    public void Validate_ValidCommand_Passes()
    {
        var command = new DeleteUserCommand(AuthTestConstants.Usernames[0]);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_EmptyUsername_Fails()
    {
        var command = new DeleteUserCommand(string.Empty);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == "Username");
    }

    [Test]
    public void Validate_UsernameTooShort_Fails()
    {
        var command = new DeleteUserCommand("aa");

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e =>
            e.PropertyName == "Username");
    }

    [Test]
    public void Validate_UsernameTooLong_Fails()
    {
        var command = new DeleteUserCommand(new string('a', 51));

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e =>
            e.PropertyName == "Username");
    }
}