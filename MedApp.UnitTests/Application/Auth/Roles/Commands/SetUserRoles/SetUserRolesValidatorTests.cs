using FluentAssertions;
using MedApp.Application.Authentication.Roles.Commands.SetUserRoles;
using MedApp.UnitTests.Common.Constants;

namespace MedApp.UnitTests.Application.Auth.Roles.Commands.SetUserRoles;

[TestFixture]
public sealed class SetUserRolesValidatorTests
{
    private SetUserRolesValidator _validator = null!;

    [SetUp]
    public void SetUp()
    {
        _validator = new SetUserRolesValidator();
    }

    [Test]
    public void Validate_ValidCommand_Passes()
    {
        var command = new SetUserRolesCommand(AuthTestConstants.Usernames[1], AuthTestConstants.Roles, AuthTestConstants.Usernames[0]);

        _validator.Validate(command).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_NoRoles_Passes()
    {
        var command = new SetUserRolesCommand(AuthTestConstants.Usernames[1], [], AuthTestConstants.Usernames[0]);

        _validator.Validate(command).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_EmptyUsername_Fails()
    {
        var command = new SetUserRolesCommand(string.Empty, AuthTestConstants.Roles, AuthTestConstants.Usernames[0]);

        var result = _validator.Validate(command);

        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(SetUserRolesCommand.Username));
    }

    [Test]
    public void Validate_NullRoles_Fails()
    {
        var command = new SetUserRolesCommand(AuthTestConstants.Usernames[1], null!, AuthTestConstants.Usernames[0]);

        var result = _validator.Validate(command);

        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(SetUserRolesCommand.Roles));
    }

    [Test]
    public void Validate_EmptyRoleName_Fails()
    {
        var command = new SetUserRolesCommand(AuthTestConstants.Usernames[1], [AuthTestConstants.Roles[0], " "], AuthTestConstants.Usernames[0]);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.ErrorMessage == "Role must not be empty.");
    }
}
