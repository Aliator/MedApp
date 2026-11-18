using FluentAssertions;
using MedApp.Application.Authentication.Roles.Commands.CreateRole;
using static MedApp.UnitTests.Common.Constants.RolesTestConstants;

namespace MedApp.UnitTests.Application.Auth.Roles.Commands.CreateRole;

[TestFixture]
public sealed class CreateRoleValidatorTests
{
    private readonly CreateRoleValidator _validator = new();

    private static CreateRoleCommand Command(string name = RoleName, string? description = Description, IReadOnlyList<string>? permissions = null) =>
        new(name, description, permissions ?? ["ViewPatients", "UseApp", "TriageReferrals"], ActorId, ActorUsername);

    [Test]
    public void Validate_Valid_HasNoErrors() =>
        _validator.Validate(Command()).IsValid.Should().BeTrue();

    [Test]
    public void Validate_NoDescriptionAndNoPermissions_IsValid() =>
        _validator.Validate(Command(description: null, permissions: [])).IsValid.Should().BeTrue();

    [TestCase("")]
    [TestCase("   ")]
    public void Validate_NoName_IsRefused(string name) =>
        _validator.Validate(Command(name)).Errors.Should().ContainSingle(e => e.ErrorMessage == "Name is required.");

    [Test]
    public void Validate_NameTooLong_IsRefused_ButSpacesAroundItDoNotCount()
    {
        _validator.Validate(Command(new string('a', 51))).Errors.Should()
            .ContainSingle(e => e.ErrorMessage == "Name must not exceed 50 characters.");
        _validator.Validate(Command("  " + new string('a', 50) + "  ")).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_DescriptionTooLong_IsRefused() =>
        _validator.Validate(Command(description: new string('a', 201))).Errors.Should()
            .ContainSingle(e => e.ErrorMessage == "Description must not exceed 200 characters.");

    [Test]
    public void Validate_NoPermissionsList_IsRefused() =>
        _validator.Validate(Command() with { Permissions = null! }).IsValid.Should().BeFalse();

    [TestCase("NotAPermission")]
    [TestCase("viewpatients")]
    [TestCase("7")]
    public void Validate_UnknownPermission_IsRefused(string name) =>
        _validator.Validate(Command(permissions: [name])).Errors.Should()
            .ContainSingle(e => e.ErrorMessage == $"'{name}' isn't a permission.");

    [TestCase("ManageUsers")]
    [TestCase("ManageRoles")]
    [TestCase("ManageSecuritySettings")]
    [TestCase("ViewAudit")]
    [TestCase("ExportPatientRecords")]
    [TestCase("ManagePrivacy")]
    [TestCase("CorrectRecords")]
    public void Validate_AdministratorOnlyPermission_IsRefused(string name) =>
        _validator.Validate(Command(permissions: ["ViewPatients", name])).Errors.Should()
            .ContainSingle(e => e.ErrorMessage == $"{name} is for the Admin role only, so no other role can have it.");
}
