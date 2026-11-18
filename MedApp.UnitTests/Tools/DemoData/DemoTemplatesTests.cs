using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.DevMcp.DemoData;
using MedApp.Infrastructure.Data.Configurations.Calendar;

namespace MedApp.UnitTests.Tools.DemoData;

/// <summary>
/// Everyone's own calendar templates in the demo (<see cref="DemoTemplates"/>): each person's weeks use only their own
/// day templates, their default is one of their weeks, the patterns differ where the story says, and the centre's
/// regular meeting times are left free for the people who go to them.
/// </summary>
[TestFixture]
public sealed class DemoTemplatesTests
{
    private static IEnumerable<StaffSeed> Everyone => DemoStaff.Roster.Prepend(DemoStaff.Lead);

    private static IEnumerable<TestCaseData> People() =>
        Everyone.Select(seed => new TestCaseData(seed).SetArgDisplayNames(seed.Username));

    private static PersonalTemplates TemplatesOf(string username) => DemoTemplates.For(Everyone.Single(s => s.Username == username));

    [TestCaseSource(nameof(People))]
    public void For_WeeksUseOnlyTheirOwnDayTemplates_AndTheDefaultIsOneOfTheirWeeks(StaffSeed seed)
    {
        var templates = DemoTemplates.For(seed);
        var days = templates.Days.Select(d => d.Name).ToList();

        days.Should().OnlyHaveUniqueItems("names are unique per owner");
        templates.Weeks.Select(w => w.Name).Should().OnlyHaveUniqueItems().And.Contain(templates.DefaultWeek);
        templates.Weeks.Should().OnlyContain(w => w.MondayToFriday.Count == 5);
        templates.Weeks.SelectMany(w => w.MondayToFriday).OfType<string>().Should().OnlyContain(name => days.Contains(name));
    }

    [TestCaseSource(nameof(People))]
    public void For_EveryBlock_IsInsideTheWorkingDay_AndNoneOverlap_AndItsTypeIsOneTheCentreHas(StaffSeed seed)
    {
        var types = CalendarActivityTypeConfiguration.Seeded.Select(t => t.Name).Concat(DemoTemplates.ActivityTypesAdded.Select(t => t.Name)).ToHashSet();

        foreach (var day in DemoTemplates.For(seed).Days)
        {
            day.Blocks.Should().OnlyContain(b => WorkingHours.WhyOutside(b.From, b.To) == null, day.Name);
            day.Blocks.Should().OnlyContain(b => types.Contains(b.Type), day.Name);
            var ordered = day.Blocks.OrderBy(b => b.From).ToList();
            for (var i = 1; i < ordered.Count; i++)
                ordered[i].From.Should().BeOnOrAfter(ordered[i - 1].To, $"{seed.Username}'s {day.Name}");
        }
    }

    [Test]
    public void For_ThePatterns_DifferWhereTheStorySays()
    {
        DayTemplateSpec Day(string username, string name) => TemplatesOf(username).Days.Single(d => d.Name == name);

        DemoTemplates.For(DemoStaff.Roster.Single(s => s.Kind == StaffKind.DiabetesNurse)).Weeks.Select(w => w.Name)
            .Should().BeEquivalentTo([DemoTemplates.FullTimeWeek, DemoTemplates.PartTimeWeek]);
        Day(DemoStaff.CoordinatorUsername, DemoTemplates.OfficeDay).Blocks.Should().Contain(b => b.Type == "Lunch" && b.From == new TimeOnly(13, 30));
        TemplatesOf(DemoStaff.SkinGpUsername).Weeks.Single().MondayToFriday[4].Should().Be(DemoTemplates.FridayList);
        TemplatesOf(DemoStaff.DermatologistUsername).Weeks.Single().MondayToFriday[4].Should().Be(DemoTemplates.AdminDay);
        TemplatesOf(DemoStaff.ManagerUsername).Weeks.Single().MondayToFriday[4].Should().Be(DemoTemplates.OfficeEarlyFinish);
        Day(DemoStaff.Lead.Username, DemoTemplates.ClinicAndManagement).Blocks.Should().Contain(b => b.Note == "Service management");
        Day(DemoTemplates.LateLunchUsername, DemoTemplates.MinorSurgeryList).Blocks.Should().Contain(DemoTemplates.ListLunchBefore);
        TemplatesOf(DemoStaff.ScientistUsername).Weeks.Single().MondayToFriday.Should().OnlyContain(d => d == DemoTemplates.LabReporting);
    }

    /// <summary>The visiting consultants are at the centre part of the week: the rest is blocked, so nothing is booked then.</summary>
    [TestCase(DemoStaff.GynaecologistUsername, 2)]
    [TestCase(DemoStaff.UrologistUsername, 2)]
    public void For_AVisitingConsultant_IsAtTheHospitalMostOfTheWeek(string username, int daysHere)
    {
        var templates = TemplatesOf(username);
        var week = templates.Weeks.Single().MondayToFriday;

        week.Count(d => d != DemoTemplates.AtTheHospital).Should().Be(daysHere);
        templates.Days.Single(d => d.Name == DemoTemplates.AtTheHospital).Blocks.Should().ContainSingle()
            .Which.Should().Match<BlockSpec>(b => b.Type == "Off site" && b.From == WorkingHours.Start && b.To == WorkingHours.End);
    }

    /// <summary>
    /// The Monday huddle (08:30) is clear on the Monday of everyone who's in then, and the skin cancer team meeting
    /// (Friday 13:30) on the Friday of everyone who goes to it.
    /// </summary>
    [Test]
    public void For_TheRegularMeetingTimes_AreClearOnTheirTemplates()
    {
        foreach (var seed in Everyone)
        {
            var templates = DemoTemplates.For(seed);
            var week = templates.Weeks.Single(w => w.Name == templates.DefaultWeek);
            bool Clear(int weekday, TimeOnly from, TimeOnly to) => templates.Days.Single(d => d.Name == week.MondayToFriday[weekday])
                .Blocks.All(b => b.To <= from || b.From >= to);

            if (week.MondayToFriday[0] != DemoTemplates.AtTheHospital)
                Clear(0, new(8, 30), new(8, 45)).Should().BeTrue($"{seed.Username}'s Monday has room for the huddle");
            if (DemoMeetings.SkinCancerTeamKinds.Contains(seed.Kind))
                Clear(4, new(13, 30), new(14, 30)).Should().BeTrue($"{seed.Username}'s Friday has room for the skin cancer team meeting");
        }
    }
}
