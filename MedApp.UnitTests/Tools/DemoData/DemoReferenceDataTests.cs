using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Common.Identity;
using MedApp.DevMcp.DemoData;
using MedApp.Domain.Patients;
using MedApp.Domain.Teams;
using static MedApp.UnitTests.Common.Constants.DemoDataTestConstants;

namespace MedApp.UnitTests.Tools.DemoData;

/// <summary>The demo data's people, patients and moments: the light demo's, synthetic, and kept to the rules.</summary>
[TestFixture]
public sealed class DemoReferenceDataTests
{
    [Test]
    public void People_AreTheLightDemosClinic_WithTheirRolesAndTeams()
    {
        var people = DemoPeople.Roster();

        people.Select(p => (p.Username, p.FullName, string.Join("+", p.Roles.Select(r => r.Name)))).Should().Equal(
            ("admin", "Centre administrator", "Admin"),
            ("n.thornbury", "Nadia Thornbury", "Clinician"),
            ("r.aldous", "Ravi Aldous", "Lab+Pathologist"),
            ("j.whitlock", "Jo Whitlock", "Manager"),
            ("b.sandiford", "Bea Sandiford", "Nurse"),
            ("h.marsh", "Hannah Marsh", "Booking"),
            ("t.hallworth", "Tom Hallworth", "Clinician"),
            ("s.varma", "Saira Varma", "Clinician"),
            ("c.ferrand", "Clara Ferrand", "Nurse"));
        DemoPeople.Thornbury.Teams.Should().Equal((DemoPeople.Dermatology, TeamMemberRole.ClinicAdmin));
        DemoPeople.Varma.Teams.Should().Equal((DemoPeople.EyeClinic, TeamMemberRole.ClinicAdmin));
        DemoPeople.Varma.JobTitle.Should().Be("Consultant ophthalmologist");
        DemoPeople.Ferrand.Teams.Should().Equal((DemoPeople.EyeClinic, TeamMemberRole.Member));
        DemoPeople.Ferrand.JobTitle.Should().Be("Ophthalmic nurse");
        foreach (var bothClinics in new[] { DemoPeople.Whitlock, DemoPeople.Marsh })
            bothClinics.Teams.Should().Equal([(DemoPeople.Dermatology, TeamMemberRole.Member), (DemoPeople.EyeClinic, TeamMemberRole.Member)],
                "the team walls need them in both clinics to assign, book and register their work");
        DemoPeople.Aldous.Teams.Should().Equal((Team.LabName, TeamMemberRole.Member));
        DemoPeople.Admin.Teams.Should().BeEmpty("an admin is in no team, and read only");
    }

    [Test]
    public void People_HaveTheirOwnDocumentationRangeAddresses() =>
        DemoPeople.Roster().Select(p => p.IpAddress).Should().OnlyHaveUniqueItems().And.OnlyContain(ip => ip.StartsWith("192.0.2."));

    [Test]
    public void People_NeverHoldTheRoleFromBeforeRoles()
    {
        DemoPeople.Roster().SelectMany(p => p.Roles).Should().NotContain(StandardRoles.AllStaffBeforeRoles);
    }

    [Test]
    public void People_ActAsTheirSessionWould_WithinTheirTeams()
    {
        var people = DemoPeople.Roster();
        var teams = new Dictionary<string, Guid> { [Team.LabName] = Team.LabId, [DemoPeople.Dermatology] = Guid.NewGuid(), [DemoPeople.EyeClinic] = Guid.NewGuid() };
        var dermatology = teams[DemoPeople.Dermatology];
        foreach (var person in people)
        {
            person.Id = Guid.NewGuid();
            person.TeamIds.AddRange(person.Teams.Select(t => teams[t.Team]));
        }

        DemoPeople.Admin.Actor.Has(Permission.WorkPathways).Should().BeFalse("admins are read only");
        DemoPeople.Admin.Actor.Teams.SeesAllTeams.Should().BeTrue();
        DemoPeople.Admin.Actor.Teams.IsSystem.Should().BeFalse("the seed works as people, never as the system");
        DemoPeople.Thornbury.Actor.Teams.TeamIds.Should().Equal(dermatology);
        DemoPeople.Thornbury.Actor.Teams.SeesAllTeams.Should().BeFalse();
        DemoPeople.Aldous.Actor.Has(Permission.AuthoriseLabReports).Should().BeTrue();
        DemoPeople.Aldous.Actor.Has(Permission.ProcessSpecimens).Should().BeTrue();
        DemoPeople.Whitlock.Actor.Has(Permission.AssignPathways).Should().BeTrue();
        DemoPeople.Varma.Actor.Teams.TeamIds.Should().Equal(teams[DemoPeople.EyeClinic]);
        DemoPeople.Varma.Actor.Has(Permission.TriageReferrals).Should().BeTrue("she triages the Eye clinic's referrals");
        DemoPeople.Varma.Actor.Has(Permission.AssignPathways).Should().BeFalse("the service manager assigns, as for Dermatology");
        people.Where(p => !p.IsAdmin).Should().OnlyContain(p => p.Actor.Has(Permission.UseApp));
    }

    [Test]
    public void Patients_HaveUniqueNames_AndValidNhsNumbersFromThe999RangeOnly()
    {
        var numbers = DemoPatients.NhsNumbers.ToList();

        numbers.Should().HaveCount(19).And.OnlyHaveUniqueItems();
        numbers.Should().OnlyContain(n => n.StartsWith("999") && NhsNumbers.IsValid(n));
        DemoPatients.All.Select(p => p.Name).Should().OnlyHaveUniqueItems();
    }

    [Test]
    public void Patients_IncludeTheEyeClinics_WithTheirSpecsNhsNumbers()
    {
        DemoPatients.All.TakeLast(4).Select(p => (p.Name, p.DateOfBirth, p.NhsNumber, p.House)).Should().Equal(
            (DemoPatients.EdithMarlow, new DateOnly(1944, 6, 17), "9991060006", 26),
            (DemoPatients.FreyaTolland, new DateOnly(1992, 2, 11), "9991061371", 27),
            (DemoPatients.HamishDewar, new DateOnly(1950, 9, 3), "9991062742", 28),
            (DemoPatients.RuthPenhaligon, new DateOnly(1966, 12, 29), "9991064117", 29));
        DemoPatients.All.Single(p => p.Name == DemoPatients.RuthPenhaligon).Practice.Should().Be(DemoSetup.ExampleLane);
    }

    [Test]
    public void Timeline_Ago_IsAUkTimeOnADayBeforeToday()
    {
        var timeline = new DemoTimeline(Now);

        timeline.Today.Should().Be(Thursday);
        timeline.ThisMonday.Should().Be(Monday);
        UkTime.DateOf(timeline.Ago(3, 14, 10)).Should().Be(Thursday.AddDays(-3));
        UkTime.TimeOf(timeline.Ago(3, 14, 10)).Should().Be(new TimeOnly(14, 10));
    }

    [TestCase(1, "2026-11-20")]
    [TestCase(2, "2026-11-23")]
    public void Timeline_OpenDayAhead_SkipsWeekends(int n, string expected) =>
        new DemoTimeline(Now).OpenDayAhead(n).Should().Be(DateOnly.Parse(expected));

    [Test]
    public void Timeline_OpenDays_SkipTheClosedBankHolidays()
    {
        var timeline = new DemoTimeline(ChristmasEve);

        timeline.OpenDayAhead(1).Should().Be(new DateOnly(2026, 12, 28));
        timeline.OpenDaysFromToday(2).Should().Equal(new DateOnly(2026, 12, 24), new DateOnly(2026, 12, 28));
    }

    [Test]
    public void Timeline_WeekdayBack_MovesAWeekendBackToFriday() =>
        new DemoTimeline(SaturdayMorning).WeekdayBack(0).Should().Be(new DateOnly(2026, 11, 13));

    [TestCase("2026-11-19T09:40:00Z", "2026-11-19")]
    [TestCase("2026-11-16T07:00:00Z", "2026-11-13")]
    [TestCase("2026-11-14T11:00:00Z", "2026-11-13")]
    public void Timeline_LastPast_IsOverBeforeNow_OnAWeekday(string now, string expected)
    {
        var timeline = new DemoTimeline(DateTime.Parse(now).ToUniversalTime());

        var visit = timeline.LastPast(9, 0, 15);

        UkTime.DateOf(visit).Should().Be(DateOnly.Parse(expected));
        visit.AddMinutes(15).Should().BeBefore(timeline.NowUtc);
    }

    [Test]
    public void Timeline_Past_KeepsTodayInThePast()
    {
        var timeline = new DemoTimeline(MondayEarly);

        timeline.Past(timeline.Ago(0, 15, 0)).Should().BeBefore(timeline.NowUtc);
        timeline.Past(timeline.Ago(1, 15, 0)).Should().Be(timeline.Ago(1, 15, 0));
    }
}
