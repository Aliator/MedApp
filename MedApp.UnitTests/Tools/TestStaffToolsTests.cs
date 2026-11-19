using FluentAssertions;
using MedApp.Application.Authentication.Users.Commands.SetUserTeams;
using MedApp.Application.Common.Identity;
using MedApp.DevMcp.Tools;
using MedApp.Domain.Teams;
using ModelContextProtocol;

namespace MedApp.UnitTests.Tools;

/// <summary>
/// <c>ensure_test_staff</c>'s accounts: staff in the right team for clinical work (admins are read only), as the light
/// demo's clinician and pathologist; and <c>ensure_test_admin</c>'s, Admin alone.
/// </summary>
[TestFixture]
public sealed class TestStaffToolsTests
{
    private static TestStaffAccount Clinician => TestStaffTools.Accounts.Single(a => a.Suffix == "clinician");

    private static TestStaffAccount Lab => TestStaffTools.Accounts.Single(a => a.Suffix == "lab");

    [Test]
    public void Accounts_AreDermatologysClinicAdminAndALabPathologist()
    {
        TestStaffTools.Accounts.Select(a => (a.UsernameFor(TestStaffTools.DefaultPrefix), a.Team, a.TeamRole, string.Join("+", a.Roles.Select(r => r.Name))))
            .Should().Equal(
                ("dev-clinician", TestStaffTeam.Dermatology, TeamMemberRole.ClinicAdmin, "Clinician+Booking"),
                ("dev-lab", TestStaffTeam.Lab, TeamMemberRole.Member, "Lab+Pathologist"));
    }

    [Test]
    public void Accounts_DoClinicalWork_AndNothingOnlyAnAdminMay()
    {
        Clinician.Permissions.Should().Contain([
            Permission.EditPatients, Permission.DeletePatients, Permission.TriageReferrals, Permission.CreatePathways,
            Permission.WorkPathways, Permission.BookAppointments, Permission.WriteLetters, Permission.SignOffResults,
        ]);
        Lab.Permissions.Should().Contain([Permission.ProcessSpecimens, Permission.AuthoriseLabReports]);

        foreach (var account in TestStaffTools.Accounts)
        {
            account.Roles.Should().NotContain(StandardRoles.Admin, "an admin is in no team, and read only");
            account.Roles.Should().NotContain(StandardRoles.AllStaffBeforeRoles);
            account.Permissions.Where(p => Permissions.Describe(p).IsAdminOnly).Should().BeEmpty();
        }
    }

    [TestCase("dev", "dev-clinician", "dev-lab")]
    [TestCase("dev-parity", "dev-parity-clinician", "dev-parity-lab")]
    [TestCase("e2e2", "e2e2-clinician", "e2e2-lab")]
    public void Usernames_AreThePrefixThenTheAccount(string prefix, string clinician, string lab)
    {
        TestStaffTools.Usernames(prefix).Should().Equal(clinician, lab);
    }

    [TestCase("")]
    [TestCase("Dev")]
    [TestCase("-dev")]
    [TestCase("1dev")]
    [TestCase("dev parity")]
    [TestCase("dev.parity")]
    public void Usernames_PrefixNotLowerCaseLettersDigitsAndHyphens_Refused(string prefix)
    {
        var usernames = () => TestStaffTools.Usernames(prefix);

        usernames.Should().Throw<McpException>().WithMessage("*isn't a usable prefix*");
    }

    [Test]
    public void RoleChanges_AddsWhatsMissingAndTakesAwayTheRest_IgnoringCase()
    {
        var (add, remove) = TestStaffTools.RoleChanges(["Admin", "clinician"], ["Clinician", "Booking"]);

        add.Should().Equal("Booking");
        remove.Should().Equal("Admin");
    }

    [Test]
    public void RoleChanges_AlreadyExact_ChangesNothing()
    {
        var (add, remove) = TestStaffTools.RoleChanges(["Lab", "Pathologist"], ["Pathologist", "Lab"]);

        add.Should().BeEmpty();
        remove.Should().BeEmpty();
    }

    [Test]
    public void Choice_ClinicAdminNamesTheRole_AMemberLeavesItOut()
    {
        var team = Guid.NewGuid();

        TestStaffTools.Choice(Clinician, team).Should().Be(new TeamChoice(team, "ClinicAdmin"));
        TestStaffTools.Choice(Lab, team).Role.Should().BeNull("the lab has no clinic admin");
    }

    [Test]
    public void TestAdmin_HoldsAdminAlone()
    {
        TestAdminTools.Roles.Should().Equal(AppRoles.Admin);
    }
}
