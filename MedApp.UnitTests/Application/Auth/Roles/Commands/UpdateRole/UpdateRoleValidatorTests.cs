using FluentAssertions;
using MedApp.Application.Authentication.Roles.Commands.UpdateRole;
using static MedApp.UnitTests.Common.Constants.RolesTestConstants;

namespace MedApp.UnitTests.Application.Auth.Roles.Commands.UpdateRole;

[TestFixture]
public sealed class UpdateRoleValidatorTests
{
    private readonly UpdateRoleValidator _validator = new();

    private static UpdateRoleCommand Command(
        string name = RoleName, string version = RoleVersion, IReadOnlyList<string>? permissions = null, Guid? roleId = null) =>
        new(roleId ?? RoleId, name, Description, permissions ?? ["ViewPatients"], version, ActorId, ActorUsername);

    [Test]
    public void Validate_Valid_HasNoErrors() =>
        _validator.Validate(Command()).IsValid.Should().BeTrue();

    [Test]
    public void Validate_NoVersion_IsRefused() =>
        _validator.Validate(Command(version: "")).Errors.Should()
            .ContainSingle(e => e.ErrorMessage == "Expected version is required: send the version you last saw.");

    [Test]
    public void Validate_NoRoleId_IsRefused() =>
        _validator.Validate(Command(roleId: Guid.Empty)).IsValid.Should().BeFalse();

    [Test]
    public void Validate_NoName_IsRefused() =>
        _validator.Validate(Command(" ")).Errors.Should().ContainSingle(e => e.ErrorMessage == "Name is required.");

    [Test]
    public void Validate_AdministratorOnlyPermission_IsRefused() =>
        _validator.Validate(Command(permissions: ["ManageRoles"])).Errors.Should()
            .ContainSingle(e => e.ErrorMessage == "ManageRoles is for the Admin role only, so no other role can have it.");

    [Test]
    public void Validate_UnknownPermission_IsRefused() =>
        _validator.Validate(Command(permissions: ["Everything"])).Errors.Should()
            .ContainSingle(e => e.ErrorMessage == "'Everything' isn't a permission.");
}
