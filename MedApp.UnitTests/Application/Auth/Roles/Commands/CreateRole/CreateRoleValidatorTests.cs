using FluentAssertions;
using MedApp.Application.Auth.Roles.Commands.CreateRole;
using MedApp.UnitTests.Common.Constants;

namespace MedApp.UnitTests.Application.Auth.Roles.Commands.CreateRole;

[TestFixture]
public sealed class CreateRoleValidatorTests
{
    private CreateRoleValidator _validator = null!;

    [SetUp]
    public void SetUp()
    {
        _validator = new CreateRoleValidator();
    }

    [Test]
    public void Validate_ValidName_Passes()
    {
        var command = new CreateRoleCommand(AuthTestConstants.Roles[0]);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_EmptyName_Fails()
    {
        var command = new CreateRoleCommand(string.Empty);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e =>
            e.PropertyName == nameof(CreateRoleCommand.Name));
    }

    [Test]
    public void Validate_NameTooLong_Fails()
    {
        var command = new CreateRoleCommand(new string('a', 51));

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e =>
            e.PropertyName == nameof(CreateRoleCommand.Name));
    }
}