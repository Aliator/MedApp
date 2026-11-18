using FluentAssertions;
using MedApp.Application.Authentication.Roles.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using static MedApp.UnitTests.Common.Constants.RolesTestConstants;

namespace MedApp.UnitTests.Application.Auth.Roles.Common;

/// <summary>RoleChange (what a change did) and RoleRules (the checks and messages).</summary>
[TestFixture]
public sealed class RoleChangeTests
{
    [Test]
    public void Between_Nothing_HasNoChange()
    {
        var change = RoleChange.Between(Role(), RoleName, Description, [Permission.ViewPatients, Permission.WorkPathways]);

        change.Any.Should().BeFalse();
        change.Details().Keys.Should().BeEquivalentTo("roleId", "role");
    }

    [Test]
    public void Between_Everything_RecordsEachPart_AndSaysItInASentence()
    {
        var change = RoleChange.Between(Role(), OtherRoleName, "Other words.", [Permission.ViewPatients, Permission.ArrangeMeetings, Permission.AttachFiles]);

        change.Any.Should().BeTrue();
        change.RenamedFrom.Should().Be(RoleName);
        change.DescriptionChanged.Should().BeTrue();
        change.Added.Should().Equal(Permission.ArrangeMeetings, Permission.AttachFiles);
        change.Removed.Should().Equal(Permission.WorkPathways);
        change.Details()["added"].Should().BeEquivalentTo(new[] { "ArrangeMeetings", "AttachFiles" });
        change.Details()["removed"].Should().BeEquivalentTo(new[] { "WorkPathways" });
        change.Details()["renamedFrom"].Should().Be(RoleName);
        change.Details()["descriptionChanged"].Should().Be(true);
        change.Summary().Should().Be(
            $"changed the role {OtherRoleName}: renamed it from {RoleName}; changed its description; added ArrangeMeetings, AttachFiles; removed WorkPathways.");
    }

    [Test]
    public void Details_NeverHoldTheDescriptionText()
    {
        var change = RoleChange.Between(Role(), RoleName, "Secret-looking new words", [Permission.ViewPatients]);

        change.Details().Values.Should().NotContain("Secret-looking new words");
        change.Summary().Should().NotContain("Secret-looking new words");
    }

    [Test]
    public void Archiving_RecordsArchivedOrRestored()
    {
        RoleChange.Archiving(Role(), true).Details()["archived"].Should().Be(true);
        RoleChange.Archiving(Role(), false).Summary().Should().Be($"changed the role {RoleName}: restored it.");
        RoleChange.Archiving(Role(), true).Any.Should().BeTrue();
    }

    [Test]
    public void Requested_KnownOnes_InTheCatalogueOrder_EachOnce_WithoutUseApp() =>
        RoleRules.Requested(["WorkPathways", " ViewPatients ", "UseApp", "WorkPathways", "Unknown"])
            .Should().Equal(Permission.ViewPatients, Permission.WorkPathways);

    [Test]
    public void EnsureEditable_TheBuiltInRole_IsAConflict()
    {
        var act = () => RoleRules.EnsureEditable(AdminRole());

        act.Should().Throw<ConflictException>().WithMessage(RoleRules.BuiltInMessage);
        RoleRules.EnsureEditable(Role());
    }

    [Test]
    public void EnsureVersion_AnotherVersion_IsAConflict_ComparingExactly()
    {
        var act = () => RoleRules.EnsureVersion(Role(), RoleVersion.ToUpperInvariant());

        act.Should().Throw<ConflictException>().WithMessage(RoleRules.ChangedMeanwhileMessage);
        RoleRules.EnsureVersion(Role(), RoleVersion);
    }

    [TestCase(1, "1 person holds the role R, so it can't be deleted. Archive it instead, or take it from them first.")]
    [TestCase(6, "6 people hold the role R, so it can't be deleted. Archive it instead, or take it from them first.")]
    public void HeldMessage_CountsPeople(int holders, string expected) =>
        RoleRules.HeldMessage("R", holders).Should().Be(expected);
}
