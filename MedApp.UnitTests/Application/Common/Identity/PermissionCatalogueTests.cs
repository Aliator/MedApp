using System.Reflection;
using FluentAssertions;
using MedApp.Application.Common.Identity;
using MedApp.Infrastructure.Migrations;
using static MedApp.Application.Common.Identity.Permission;

namespace MedApp.UnitTests.Application.Common.Identity;

/// <summary>
/// The permission catalogue, the built-in Admin role's fixed set and the starting roles, pinned to decision 68's table
/// in <c>docs/centre-phase2-plan.md</c>. A change to any of them changes what people can do: change the table too.
/// </summary>
[TestFixture]
public sealed class PermissionCatalogueTests
{
    private static readonly Permission[] AdministratorOnly =
        [ManageUsers, ManageRoles, ManageSecuritySettings, ViewAudit, ExportPatientRecords, ManagePrivacy, CorrectRecords];

    [Test]
    public void Catalogue_DescribesEveryPermissionOnce_WithAGroupAndADescription()
    {
        Permissions.Catalogue.Select(p => p.Permission).Should().BeEquivalentTo(Enum.GetValues<Permission>());
        Permissions.Catalogue.Select(p => p.Permission).Should().OnlyHaveUniqueItems();

        foreach (var permission in Permissions.Catalogue)
        {
            permission.Description.Should().NotBeNullOrWhiteSpace();
            permission.Description.Should().EndWith(".");
            Enum.IsDefined(permission.Group).Should().BeTrue();
            Permissions.Describe(permission.Permission).Should().BeSameAs(permission);
        }
    }

    [Test]
    public void AdministratorOnlyGroup_IsExactlyTheAdministrationPermissions()
    {
        Permissions.Catalogue.Where(p => p.IsAdminOnly).Select(p => p.Permission).Should().BeEquivalentTo(AdministratorOnly);
    }

    [Test]
    public void OnlyUseApp_IsTheBase()
    {
        Permissions.Catalogue.Where(p => p.Group == PermissionGroup.Base).Select(p => p.Permission).Should().Equal(UseApp);
    }

    /// <summary>Column A: every permission today's admins use, which is all but triage.</summary>
    [Test]
    public void Admin_HasEveryPermissionButTriage()
    {
        Permissions.Admin.Should().BeEquivalentTo(
        [
            UseApp, ViewPatients, EditPatients, DeletePatients, RegisterReferrals, CreatePathways, WorkPathways,
            AssignPathways, OverrideStages, DeletePathways, BookAppointments, ManageAnyCalendar, ArrangeMeetings,
            AttachFiles, ViewInsights, ManageMonitoring, ManageSetup, ManageUsers, ManageRoles, ManageSecuritySettings,
            ViewAudit, ExportPatientRecords, ManagePrivacy, CorrectRecords,
        ]);
        Permissions.Admin.Should().NotContain(TriageReferrals);
    }

    [Test]
    public void StandardRoles_MatchTheTable()
    {
        StandardRoles.AllStaffBeforeRoles.Permissions.Should().BeEquivalentTo(
            [UseApp, ViewPatients, EditPatients, DeletePatients, RegisterReferrals, CreatePathways, WorkPathways, BookAppointments, AttachFiles]);
        StandardRoles.Clinician.Permissions.Should().BeEquivalentTo(
            [UseApp, ViewPatients, EditPatients, RegisterReferrals, TriageReferrals, CreatePathways, WorkPathways, OverrideStages, BookAppointments, AttachFiles]);
        StandardRoles.Nurse.Permissions.Should().BeEquivalentTo(
            [UseApp, ViewPatients, EditPatients, RegisterReferrals, CreatePathways, WorkPathways, BookAppointments, AttachFiles]);
        StandardRoles.Booking.Permissions.Should().BeEquivalentTo(
            [UseApp, ViewPatients, EditPatients, DeletePatients, RegisterReferrals, BookAppointments, ManageAnyCalendar, AttachFiles]);
        StandardRoles.Lab.Permissions.Should().BeEquivalentTo([UseApp, ViewPatients, WorkPathways, AttachFiles]);
        StandardRoles.Manager.Permissions.Should().BeEquivalentTo(
            [UseApp, ViewPatients, AssignPathways, ArrangeMeetings, ViewInsights, ManageMonitoring]);
    }

    [Test]
    public void EditableStandardRoles_HaveNoAdministratorOnlyPermission_AndUseApp()
    {
        foreach (var role in StandardRoles.All.Where(r => r != StandardRoles.Admin))
        {
            role.Permissions.Should().NotIntersectWith(AdministratorOnly, role.Name);
            role.Permissions.Should().Contain(UseApp, role.Name);
        }
    }

    [Test]
    public void StandardRoles_HaveUniqueIdsAndNames()
    {
        StandardRoles.All.Select(r => r.Id).Should().OnlyHaveUniqueItems();
        StandardRoles.All.Select(r => r.Name.ToUpperInvariant()).Should().OnlyHaveUniqueItems();
        StandardRoles.Admin.Name.Should().Be(AppRoles.Admin);
        StandardRoles.AllStaffBeforeRoles.Name.Should().Be(AppRoles.AllStaffBeforeRoles);
    }

    [Test]
    public void GrantedBy_BuiltInAdmin_IsTheFixedSet_WhateverIsStored()
    {
        Permissions.GrantedBy(isBuiltInAdmin: true, ["TriageReferrals"]).Should().BeEquivalentTo(Permissions.Admin);
    }

    [Test]
    public void GrantedBy_EditableRole_IsItsKnownPermissionsAndUseApp()
    {
        Permissions.GrantedBy(isBuiltInAdmin: false, ["ViewPatients", "WorkPathways"])
            .Should().BeEquivalentTo([UseApp, ViewPatients, WorkPathways]);
    }

    [Test]
    public void GrantedBy_EditableRole_IgnoresUnknownNamesNumbersAndOtherCases()
    {
        Permissions.GrantedBy(isBuiltInAdmin: false, ["RemovedLongAgo", "1", "viewpatients", null, ""])
            .Should().BeEquivalentTo([UseApp]);
    }

    [Test]
    public void GrantedBy_EditableRole_NeverGrantsAnAdministratorOnlyPermission()
    {
        Permissions.GrantedBy(isBuiltInAdmin: false, AdministratorOnly.Select(p => p.ToString()))
            .Should().BeEquivalentTo([UseApp]);
    }

    [Test]
    public void GrantedBy_Roles_AddUp_AndNoRoleIsNothing()
    {
        Permissions.GrantedBy([StandardRoles.Lab.Permissions, StandardRoles.Manager.Permissions])
            .Should().BeEquivalentTo(StandardRoles.Lab.Permissions.Union(StandardRoles.Manager.Permissions));
        Permissions.GrantedBy([]).Should().BeEmpty();
    }

    [TestCase("ViewPatients", true)]
    [TestCase("viewPatients", false)]
    [TestCase("0", false)]
    [TestCase(null, false)]
    public void TryParse_ExactNamesOnly(string? name, bool known)
    {
        Permissions.TryParse(name, out _).Should().Be(known);
    }

    /// <summary>
    /// The migration writes its values itself (so it never changes after it ships). Every name it writes is one code
    /// knows, and its roles are the standard ones, so a new database and a migrated one agree.
    /// </summary>
    [Test]
    public void Migration_WritesTheStandardRoles_WithKnownPermissionNames()
    {
        const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Static;
        var migration = typeof(AddedRolePermissions);
        var allStaff = (string[])migration.GetField("AllStaffPermissions", Private)!.GetValue(null)!;
        var starting = ((string Id, string Name, string Description, string[] Permissions)[])migration.GetField("StartingRoles", Private)!.GetValue(null)!;

        foreach (var name in allStaff.Concat(starting.SelectMany(r => r.Permissions)))
            Permissions.TryParse(name, out _).Should().BeTrue(name);

        allStaff.Should().BeEquivalentTo(StandardRoles.AllStaffBeforeRoles.Permissions.Select(p => p.ToString()));
        starting.Select(r => (Guid.Parse(r.Id), r.Name, r.Description)).Should().Equal(
            StandardRoles.Starting.Select(r => (r.Id, r.Name, r.Description)));
        foreach (var (role, standard) in starting.Zip(StandardRoles.Starting))
            role.Permissions.Should().BeEquivalentTo(standard.Permissions.Select(p => p.ToString()), standard.Name);

        ((string)migration.GetField("AdminId", Private)!.GetValue(null)!).Should().Be(StandardRoles.Admin.Id.ToString());
        ((string)migration.GetField("AllStaffId", Private)!.GetValue(null)!).Should().Be(StandardRoles.AllStaffBeforeRoles.Id.ToString());
        ((string)migration.GetField("AdminDescription", Private)!.GetValue(null)!).Should().Be(StandardRoles.Admin.Description);
        ((string)migration.GetField("AllStaffDescription", Private)!.GetValue(null)!).Should().Be(StandardRoles.AllStaffBeforeRoles.Description);
    }
}
