using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.DevMcp.DemoData;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.UnitTests.Common.Constants;
using static MedApp.UnitTests.Common.Constants.DemoDataTestConstants;

namespace MedApp.UnitTests.Tools.DemoData;

/// <summary>
/// The demo data's calendars (<see cref="DemoCalendars"/>), planned in memory at many moments, since the story is told
/// relative to "now": every stored block obeys the app's no-overlap rule, and the pharmacist's freed slot is reserved
/// once it's booked, so no visit is booked over it.
/// </summary>
[TestFixture]
public sealed class DemoCalendarsTests
{
    [TestCaseSource(typeof(DemoDataTestConstants), nameof(CalendarMoments))]
    public void Plan_EveryStoredBlock_OverlapsNothingElseOnThatPersonsCalendar(DateTime nowUtc)
    {
        var timeline = new DemoTimeline(nowUtc);
        var calendars = DemoCalendars.Plan(Staff(timeline), timeline, new Random(Seed));

        calendars.StoredBlocks.Should().HaveCountGreaterThan(50);
        foreach (var block in calendars.StoredBlocks)
        {
            var date = UkTime.DateOf(block.StartUtc);
            CalendarClashes.Find(calendars.DataFor(block.StaffUserId, date, date), block.StartUtc, block.EndUtc, ignoreBlockId: block.Id)
                .Should().BeNull($"the block on {UkTime.Describe(block.StartUtc)} to {UkTime.DescribeTime(block.EndUtc)} is stored, as the app would allow");
        }
    }

    [TestCaseSource(typeof(DemoDataTestConstants), nameof(CalendarMoments))]
    public void Plan_EveryDayOfLeaveOrTraining_HasNoDayTemplate_AndOneOverridePerDay(DateTime nowUtc)
    {
        var timeline = new DemoTimeline(nowUtc);
        var calendars = DemoCalendars.Plan(Staff(timeline), timeline, new Random(Seed));

        var wholeDays = calendars.StoredBlocks.Where(b => (b.EndUtc - b.StartUtc).TotalHours >= 9).ToList();
        wholeDays.Should().NotBeEmpty();
        foreach (var block in wholeDays)
            calendars.DayOverrides.Should().ContainSingle(o => o.StaffUserId == block.StaffUserId && o.Date == UkTime.DateOf(block.StartUtc))
                .Which.DayTemplateId.Should().BeNull("a whole day can only be blocked once the day has no template");

        calendars.DayOverrides.GroupBy(o => (o.StaffUserId, o.Date)).Should().OnlyContain(g => g.Count() == 1);
    }

    [TestCaseSource(typeof(DemoDataTestConstants), nameof(CalendarMoments))]
    public void Plan_EveryTemplateHasAnOwner_WhoseWeeksAndDefaultUseOnlyTheirOwn(DateTime nowUtc)
    {
        var timeline = new DemoTimeline(nowUtc);
        var staff = Staff(timeline);
        var calendars = DemoCalendars.Plan(staff, timeline, new Random(Seed));
        var days = calendars.DayTemplateRows.ToDictionary(d => d.Id);
        var weeks = calendars.WeekTemplateRows.ToDictionary(w => w.Id);

        days.Values.Should().OnlyContain(d => staff.Any(s => s.Id == d.OwnerUserId));
        weeks.Values.Should().OnlyContain(w => staff.Any(s => s.Id == w.OwnerUserId));
        days.Values.GroupBy(d => (d.OwnerUserId, d.Name)).Should().OnlyContain(g => g.Count() == 1, "names are unique per owner");
        foreach (var week in weeks.Values)
            week.Days.Should().OnlyContain(d => days[d.DayTemplateId].OwnerUserId == week.OwnerUserId, $"week template '{week.Name}' uses only its owner's day templates");
        calendars.DefaultWeeks.Should().OnlyContain(d => weeks[d.WeekTemplateId].OwnerUserId == d.StaffUserId);
        calendars.DayOverrides.Where(o => o.DayTemplateId != null)
            .Should().OnlyContain(o => days[o.DayTemplateId!.Value].OwnerUserId == o.StaffUserId);
        staff.Should().OnlyContain(s => calendars.DefaultWeeks.Any(d => d.StaffUserId == s.Id), "everyone has a default week");
    }

    [Test]
    public void Plan_HistoryChangesPartWay_ThePartTimeWeekAndTheDermatologistsLaterLunch()
    {
        var timeline = new DemoTimeline(Now);
        var staff = Staff(timeline);
        var calendars = DemoCalendars.Plan(staff, timeline, new Random(Seed));
        var partTime = staff.Single(s => s.Kind == StaffKind.DiabetesNurse);
        var dermatologist = DemoStaff.Get(staff, DemoTemplates.LateLunchUsername);
        var changed = timeline.ThisMonday.AddDays(-7 * 20);

        calendars.DefaultWeeks.Where(d => d.StaffUserId == partTime.Id).OrderBy(d => d.ValidFrom).Select(d => (d.ValidTo, d.ValidFrom))
            .Should().HaveCount(2).And.Satisfy(d => d.ValidTo == changed.AddDays(-1), d => d.ValidFrom == changed && d.ValidTo == null);

        // Tuesday list days (not on leave, nor bank holidays, when no template applies) before the change, and after it:
        // the lunch moved later.
        var tuesdays = Enumerable.Range(1, 20).Select(w => timeline.ThisMonday.AddDays(1 - 7 * w))
            .Where(d => !calendars.IsOnLeave(dermatologist.Id, d) && DemoClosedDays.IsWorkingDay(d)).ToList();
        LunchOn(tuesdays.First(d => d < timeline.ThisMonday.AddDays(-7 * 10))).Should().Be(new TimeOnly(12, 45));
        LunchOn(tuesdays.First(d => d >= timeline.ThisMonday.AddDays(-7 * 10))).Should().Be(new TimeOnly(13, 15));

        TimeOnly? LunchOn(DateOnly tuesday)
        {
            var data = calendars.DataFor(dermatologist.Id, tuesday, tuesday);
            return CalendarProjection.TemplateBlocks(data)
                .Where(b => b.Source.Note != "List preparation" && b.Source.Note != "Trainee supervision")
                .Select(b => (TimeOnly?)UkTime.TimeOf(b.StartUtc)).SingleOrDefault();
        }
    }

    [Test]
    public void Plan_DiaryDayIsAThursday_MovesTheDermatologistsFridayLunchIntoFreeTime()
    {
        var timeline = new DemoTimeline(UkTime.ToUtc(new DateOnly(2026, 11, 19), new TimeOnly(10, 0)));
        var staff = Staff(timeline);
        var dermatologist = DemoStaff.Get(staff, DemoStaff.DermatologistUsername);
        var calendars = DemoCalendars.Plan(staff, timeline, new Random(Seed));
        var friday = new DateOnly(2026, 11, 20);

        var moved = calendars.StoredBlocks.Should().ContainSingle(b => b.StaffUserId == dermatologist.Id && UkTime.DateOf(b.StartUtc) == friday).Subject;
        CalendarClashes.Find(calendars.DataFor(dermatologist.Id, friday, friday), moved.StartUtc, moved.EndUtc, ignoreBlockId: moved.Id)
            .Should().BeNull("the Friday's admin day has its admin time and the skin cancer team meeting, and the lunch goes round them");
        CalendarProjection.TemplateBlocks(calendars.DataFor(dermatologist.Id, friday, friday))
            .Should().NotContain(p => p.StartUtc == UkTime.ToUtc(friday, new TimeOnly(12, 0)), "the template's lunch is left off that day");
    }

    [TestCaseSource(typeof(DemoDataTestConstants), nameof(CalendarMoments))]
    public void BookFreedSlot_BooksThePharmacistsFreedTime_AndReservesIt(DateTime nowUtc)
    {
        var timeline = new DemoTimeline(nowUtc);
        var staff = Staff(timeline);
        var world = new DemoWorld
        {
            Timeline = timeline,
            Random = new Random(Seed),
            Calendars = DemoCalendars.Plan(staff, timeline, new Random(Seed)),
            Staff = staff,
            Templates = new Dictionary<PathwayKind, PathwayTemplate>(),
            Definitions = new Dictionary<string, PatientPathwayStageDefinition>(),
            Rules = [],
            Teams = new Dictionary<Guid, MedApp.Domain.Teams.Team>(),
        };
        var patients = Enumerable.Range(0, 5).Select(i => new PatientSeed
        {
            Id = Guid.NewGuid(),
            FirstName = "Synthetic",
            LastName = $"Patient{i}",
            DateOfBirth = new DateOnly(1970, 1, 1),
            Email = $"synthetic.patient{i}@example.com",
            CreatedUtc = timeline.AppStartUtc,
            Role = PatientRole.Active,
        }).ToList();
        var (pharmacist, start, end) = world.Calendars.FreedSlot!.Value;

        DemoPathways.BookFreedSlot(world, patients);

        world.Appointments.Should().ContainSingle(a => a.StaffUserId == pharmacist && a.StartUtc == start && a.EndUtc == end);
        world.Allocator.IsFree(pharmacist, start, end).Should().BeFalse("a booked time is reserved, so no visit can be booked over it");
        world.Allocator.FreeStarts(pharmacist, UkTime.DateOf(start), 15, start.AddHours(-1))
            .Should().NotContain(s => CalendarClashes.Overlaps(s, s.AddMinutes(15), start, end));
    }
}
