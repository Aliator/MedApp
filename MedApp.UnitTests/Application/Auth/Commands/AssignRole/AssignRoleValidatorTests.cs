using FluentAssertions;
using MedApp.Application.Auth.Commands.AssignRole;

namespace MedApp.UnitTests.Application.Auth.Commands.AssignRole;

[TestFixture]
public sealed class AssignRoleValidatorTests
{
    private AssignRoleValidator _validator = null!;

    [SetUp]
    public void SetUp()
    {
        _validator = new AssignRoleValidator();
    }

    [Test]
    public void Validate_ValidCommand_Passes()
    {
        var command = new AssignRoleCommand("username", "Role");

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_EmptyUsername_Fails()
    {
        var command = new AssignRoleCommand(string.Empty, "Role");

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e =>
            e.PropertyName == "Username");
    }

    [Test]
    public void Validate_EmptyRole_Fails()
    {
        var command = new AssignRoleCommand("username", string.Empty);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e =>
            e.PropertyName == "Role");
    }
}