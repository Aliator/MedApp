using FluentAssertions;
using MedApp.Application.Auth.Commands.DeleteRole;
using MedApp.UnitTests.Common.Constants;

namespace MedApp.UnitTests.Application.Auth.Commands.DeleteRole;

[TestFixture]
public sealed class DeleteRoleValidatorTests
{
    private DeleteRoleValidator _validator = null!;

    [SetUp]
    public void SetUp()
    {
        _validator = new DeleteRoleValidator();
    }

    [Test]
    public void Validate_ValidCommand_Passes()
    {
        var command = new DeleteRoleCommand(AuthTestConstants.Roles[0]);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_EmptyRoleName_Fails()
    {
        var command = new DeleteRoleCommand(string.Empty);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e =>
            e.PropertyName == "RoleName");
    }

    [Test]
    public void Validate_RoleNameTooLong_Fails()
    {
        var command = new DeleteRoleCommand(new string('a', 51));

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e =>
            e.PropertyName == "RoleName");
    }
}