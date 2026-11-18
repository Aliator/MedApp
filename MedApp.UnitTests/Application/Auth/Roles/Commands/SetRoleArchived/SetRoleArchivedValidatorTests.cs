using FluentAssertions;
using MedApp.Application.Authentication.Roles.Commands.SetRoleArchived;
using static MedApp.UnitTests.Common.Constants.RolesTestConstants;

namespace MedApp.UnitTests.Application.Auth.Roles.Commands.SetRoleArchived;

[TestFixture]
public sealed class SetRoleArchivedValidatorTests
{
    private readonly SetRoleArchivedValidator _validator = new();

    [Test]
    public void Validate_Valid_HasNoErrors() =>
        _validator.Validate(new SetRoleArchivedCommand(RoleId, true, RoleVersion, ActorId, ActorUsername)).IsValid.Should().BeTrue();

    [Test]
    public void Validate_NoVersion_IsRefused() =>
        _validator.Validate(new SetRoleArchivedCommand(RoleId, true, "", ActorId, ActorUsername)).Errors.Should()
            .ContainSingle(e => e.ErrorMessage == "Expected version is required: send the version you last saw.");

    [Test]
    public void Validate_NoRoleId_IsRefused() =>
        _validator.Validate(new SetRoleArchivedCommand(Guid.Empty, true, RoleVersion, ActorId, ActorUsername)).Errors.Should()
            .ContainSingle(e => e.ErrorMessage == "Role id is required.");
}
