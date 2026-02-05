using FluentAssertions;
using MedApp.Application.Auth.Roles.Commands.RevokeRole;
using MedApp.UnitTests.Common.Constants;

namespace MedApp.UnitTests.Application.Auth.Roles.Commands.RevokeRole;

[TestFixture]
public sealed class RevokeRoleValidatorTests
{
    private RevokeRoleValidator _validator = null!;

    [SetUp]
    public void SetUp()
    {
        _validator = new RevokeRoleValidator();
    }

    [Test]
    public void Validate_ValidCommand_Passes()
    {
        var command = new RevokeRoleCommand(AuthTestConstants.Usernames[0], AuthTestConstants.Roles[0]);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_EmptyUsername_Fails()
    {
        var command = new RevokeRoleCommand(string.Empty, AuthTestConstants.Roles[0]);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e =>
            e.PropertyName == nameof(RevokeRoleCommand.Username));
    }

    [Test]
    public void Validate_EmptyRole_Fails()
    {
        var command = new RevokeRoleCommand(AuthTestConstants.Usernames[0], string.Empty);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e =>
            e.PropertyName == nameof(RevokeRoleCommand.Role));
    }
}