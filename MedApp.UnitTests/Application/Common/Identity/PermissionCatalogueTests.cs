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

    /// <summary>
    /// Column A: every permission but the clinical ones: triage, finishing a stage without its visit, recording and
    /// processing specimens, and letters (<c>docs/centre-phases-3-5-plan.md</c>, decisions 74 and 138).
    /// </summary>
    [Test]
    public void Admin_HasEveryPermissionButTheClinicalOnes()
    {
        Permissions.Admin.Should().BeEquivalentTo(
        [
            UseApp, ViewPatients, EditPatients, DeletePatients, RegisterReferrals, CreatePathways, WorkPathways,
            AssignPathways, UnlockStages, DeletePathways, BookAppointments, ManageAnyCalendar, ArrangeMeetings,
            ManageClinics, AttachFiles, ViewInsights, ManageMonitoring, ManageSetup, ManageUsers, ManageRoles, ManageSecuritySettings,
            ViewAudit, ExportPatientRecords, ManagePrivacy, CorrectRecords,
        ]);
        Permissions.Admin.Should().NotContain(TriageReferrals).And.NotContain(FinishWithoutVisit);
        Permissions.Admin.Should().NotContain([WriteLetters, ApproveLetters, SendLetters]);
        Permissions.Admin.Should().NotContain(RecordSpecimens).And.NotContain(ProcessSpecimens);
        Permissions.Admin.Should().NotContain(RecordMdtOutcomes).And.NotContain(SignOffResults);
    }

    [Test]
    public void StandardRoles_MatchTheTable()
    {
        StandardRoles.AllStaffBeforeRoles.Permissions.Should().BeEquivalentTo(
            [UseApp, ViewPatients, EditPatients, DeletePatients, RegisterReferrals, CreatePathways, WorkPathways, BookAppointments, AttachFiles]);
        StandardRoles.Clinician.Permissions.Should().BeEquivalentTo(
            [UseApp, ViewPatients, EditPatients, RegisterReferrals, TriageReferrals, CreatePathways, WorkPathways, UnlockStages, FinishWithoutVisit, RecordMdtOutcomes, BookAppointments, AttachFiles,
             RecordSpecimens, SignOffResults, WriteLetters, ApproveLetters, SendLetters]);
        StandardRoles.SeniorClinician.Permissions.Should().BeEquivalentTo(
            StandardRoles.Clinician.Permissions.Union([UnlockStages, FinishWithoutVisit]));
        StandardRoles.Nurse.Permissions.Should().BeEquivalentTo(
            [UseApp, ViewPatients, EditPatients, RegisterReferrals, CreatePathways, WorkPathways, BookAppointments, AttachFiles, RecordSpecimens, WriteLetters, SendLetters]);
        StandardRoles.Booking.Permissions.Should().BeEquivalentTo(
            [UseApp, ViewPatients, EditPatients, DeletePatients, RegisterReferrals, BookAppointments, ManageAnyCalendar, ManageClinics, AttachFiles, SendLetters]);
        StandardRoles.Lab.Permissions.Should().BeEquivalentTo([UseApp, ViewPatients, WorkPathways, AttachFiles, RecordSpecimens, ProcessSpecimens]);
        StandardRoles.Manager.Permissions.Should().BeEquivalentTo(
            [UseApp, ViewPatients, AssignPathways, ArrangeMeetings, ManageClinics, ViewInsights, ManageMonitoring]);
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

        // Every name is one code knows, but OverrideStages, which SplitOverrideStages renamed to its two halves.
        static IEnumerable<string> AsNow(IEnumerable<string> names) =>
            names.SelectMany(n => n == "OverrideStages" ? [nameof(UnlockStages), nameof(FinishWithoutVisit)] : new[] { n });
        foreach (var name in AsNow(allStaff.Concat(starting.SelectMany(r => r.Permissions))))
            Permissions.TryParse(name, out _).Should().BeTrue(name);

        allStaff.Should().BeEquivalentTo(StandardRoles.AllStaffBeforeRoles.Permissions.Select(p => p.ToString()));
        // Senior clinician came later, with SplitOverrideStages.
        var startedWith = StandardRoles.Starting.Where(r => r != StandardRoles.SeniorClinician).ToList();
        starting.Select(r => (Guid.Parse(r.Id), r.Name, r.Description)).Should().Equal(
            startedWith.Select(r => (r.Id, r.Name, r.Description)));
        // Later migrations add the permissions that came after (AddedWorkingHours: ManageClinics; AddedLetters: the
        // letter permissions; AddedSpecimens: RecordSpecimens and ProcessSpecimens; AddedMdtItems: RecordMdtOutcomes; AddedResults: SignOffResults).
        foreach (var (role, standard) in starting.Zip(startedWith))
            AsNow(role.Permissions).Should().BeEquivalentTo(
                standard.Permissions.Where(p => p is not (ManageClinics or WriteLetters or ApproveLetters or SendLetters or RecordSpecimens or ProcessSpecimens or RecordMdtOutcomes or SignOffResults)).Select(p => p.ToString()),
                standard.Name);

        ((string)migration.GetField("AdminId", Private)!.GetValue(null)!).Should().Be(StandardRoles.Admin.Id.ToString());
        ((string)migration.GetField("AllStaffId", Private)!.GetValue(null)!).Should().Be(StandardRoles.AllStaffBeforeRoles.Id.ToString());
        ((string)migration.GetField("AdminDescription", Private)!.GetValue(null)!).Should().Be(StandardRoles.Admin.Description);
        ((string)migration.GetField("AllStaffDescription", Private)!.GetValue(null)!).Should().Be(StandardRoles.AllStaffBeforeRoles.Description);
    }

    [Test]
    public void WorkingHoursMigration_GivesManageClinicsToBookingAndManager_AsTheStandardRolesHaveIt()
    {
        const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Static;
        var migration = typeof(AddedWorkingHours);

        ((string)migration.GetField("Permission", Private)!.GetValue(null)!).Should().Be(nameof(ManageClinics));
        ((string)migration.GetField("BookingId", Private)!.GetValue(null)!).Should().Be(StandardRoles.Booking.Id.ToString());
        ((string)migration.GetField("ManagerId", Private)!.GetValue(null)!).Should().Be(StandardRoles.Manager.Id.ToString());
        StandardRoles.Starting.Where(r => r.Permissions.Contains(ManageClinics))
            .Should().BeEquivalentTo([StandardRoles.Booking, StandardRoles.Manager]);
    }

    [Test]
    public void SpecimensMigration_GivesTheSpecimenPermissions_AsTheStandardRolesHaveThem()
    {
        const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Static;
        var migration = typeof(AddedSpecimens);
        var grants = ((string Permission, string[] RoleIds)[])migration.GetField("Grants", Private)!.GetValue(null)!;

        foreach (var permission in new[] { RecordSpecimens, ProcessSpecimens })
        {
            var holders = StandardRoles.All.Where(r => r != StandardRoles.Admin && r.Permissions.Contains(permission));
            grants.Single(g => g.Permission == permission.ToString()).RoleIds
                .Should().BeEquivalentTo(holders.Select(r => r.Id.ToString()), permission.ToString());
        }
    }

    [Test]
    public void ResultsMigration_GivesSignOffResults_AsTheStandardRolesHaveIt()
    {
        const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Static;
        var migration = typeof(AddedResults);
        string Field(string name) => (string)migration.GetField(name, Private)!.GetValue(null)!;

        Field("Permission").Should().Be(nameof(SignOffResults));
        var holders = StandardRoles.All.Where(r => r != StandardRoles.Admin && r.Permissions.Contains(SignOffResults));
        new[] { Field("ClinicianId"), Field("SeniorClinicianId") }
            .Should().BeEquivalentTo(holders.Select(r => r.Id.ToString()));
    }

    /// <summary>
    /// SplitOverrideStages writes Senior clinician as the standard role has it, and the two names it splits
    /// OverrideStages into are the permissions'.
    /// </summary>
    [Test]
    public void SplitOverrideStagesMigration_WritesSeniorClinician_AsTheStandardRoleHasIt()
    {
        const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Static;
        var migration = typeof(SplitOverrideStages);
        string Field(string name) => (string)migration.GetField(name, Private)!.GetValue(null)!;

        Field("UnlockStages").Should().Be(nameof(UnlockStages));
        Field("FinishWithoutVisit").Should().Be(nameof(FinishWithoutVisit));
        Field("SeniorClinicianId").Should().Be(StandardRoles.SeniorClinician.Id.ToString());
        Field("SeniorClinicianName").Should().Be(StandardRoles.SeniorClinician.Name);
        Field("SeniorClinicianDescription").Should().Be(StandardRoles.SeniorClinician.Description);
        // AddedMdtItems gives it RecordMdtOutcomes later.
        ((string[])migration.GetField("SeniorClinicianPermissions", Private)!.GetValue(null)!)
            .Should().BeEquivalentTo(StandardRoles.SeniorClinician.Permissions
                .Where(p => p is not (RecordSpecimens or ProcessSpecimens or WriteLetters or ApproveLetters or SendLetters or RecordMdtOutcomes or SignOffResults)).Select(p => p.ToString()),
                "AddedLetters, AddedSpecimens, AddedMdtItems and AddedResults add theirs later");
    }

    /// <summary>AddedMdtItems gives RecordMdtOutcomes to Clinician and Senior clinician, as the standard roles have it.</summary>
    [Test]
    public void MdtMigration_GivesRecordMdtOutcomesToClinicianAndSeniorClinician_AsTheStandardRolesHaveIt()
    {
        const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Static;
        var migration = typeof(AddedMdtItems);

        ((string)migration.GetField("Permission", Private)!.GetValue(null)!).Should().Be(nameof(RecordMdtOutcomes));
        ((string[])migration.GetField("RoleIds", Private)!.GetValue(null)!).Should().BeEquivalentTo(
            StandardRoles.Starting.Where(r => r.Permissions.Contains(RecordMdtOutcomes)).Select(r => r.Id.ToString()));
    }
}
