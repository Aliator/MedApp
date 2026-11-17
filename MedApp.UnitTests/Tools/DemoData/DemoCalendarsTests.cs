using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.DevMcp.DemoData;
using MedApp.Domain.Procedures.PatientProcedures;
using static MedApp.UnitTests.Common.Constants.DemoDataTestConstants;

namespace MedApp.UnitTests.Tools.DemoData;

/// <summary>
/// The demo data's calendars (<see cref="DemoCalendars"/>), planned in memory at many moments, since the story is told
/// relative to "now": every stored block obeys the app's no-overlap rule, and the nurse's freed slot is reserved once
/// it's booked, so no visit is booked over it.
/// </summary>
[TestFixture]
public sealed class DemoCalendarsTests
{
    /// <summary>
    /// Moments that change the calendars' shape (UK time): each weekday (the diary day and the days either side), a
    /// weekend, late on a Sunday and just after midnight, both 2026 clock changes, and a new year.
    /// </summary>
    private static IEnumerable<TestCaseData> Moments() =>
        new (string Name, DateTime Uk)[]
        {
            ("Monday afternoon", new(2026, 11, 16, 14, 0, 0)),
            ("Tuesday morning", new(2026, 11, 17, 10, 0, 0)),
            ("Wednesday morning", new(2026, 11, 18, 10, 0, 0)),
            ("Thursday morning (the lunch moves to a Friday admin day)", new(2026, 11, 19, 10, 0, 0)),
            ("Friday afternoon", new(2026, 11, 20, 16, 30, 0)),
            ("Saturday morning", new(2026, 11, 14, 11, 0, 0)),
            ("Sunday night", new(2026, 11, 15, 23, 30, 0)),
            ("Monday just after midnight", new(2026, 11, 16, 0, 20, 0)),
            ("Sunday of the autumn clock change", new(2026, 10, 25, 10, 0, 0)),
            ("Monday after the autumn clock change", new(2026, 10, 26, 7, 30, 0)),
            ("Monday after the spring clock change", new(2026, 3, 30, 8, 10, 0)),
            ("New year", new(2027, 1, 4, 9, 0, 0)),
        }.Select(m => new TestCaseData(UkTime.ToUtc(DateOnly.FromDateTime(m.Uk), TimeOnly.FromDateTime(m.Uk))).SetArgDisplayNames(m.Name));

    /// <summary>The clinic's staff as the seed makes them, with when each started and left, but no accounts.</summary>
    private static List<StaffMember> Staff(DemoTimeline timeline) =>
        DemoStaff.Roster
            .Select(seed => new StaffMember(seed, Guid.NewGuid())
            {
                StartedUtc = timeline.AccountCreatedUtc(seed.Kind),
                LeftUtc = seed.Kind == StaffKind.Leaver ? timeline.LeaverLeftUtc : null,
            })
            .Prepend(new StaffMember(DemoStaff.Lead, Guid.NewGuid())
            {
                StartedUtc = timeline.AppStartUtc.AddDays(-30),
                AdminFromUtc = DateTime.MinValue,
            })
            .ToList();

    [TestCaseSource(nameof(Moments))]
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

    [TestCaseSource(nameof(Moments))]
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

    [Test]
    public void Plan_DiaryDayIsAThursday_MovesTheSurgeonsFridayLunchIntoFreeTime()
    {
        var timeline = new DemoTimeline(UkTime.ToUtc(new DateOnly(2026, 11, 19), new TimeOnly(10, 0)));
        var staff = Staff(timeline);
        var surgeon = DemoStaff.Get(staff, "r.calloway");
        var calendars = DemoCalendars.Plan(staff, timeline, new Random(Seed));
        var friday = new DateOnly(2026, 11, 20);

        var moved = calendars.StoredBlocks.Should().ContainSingle(b => b.StaffUserId == surgeon.Id && UkTime.DateOf(b.StartUtc) == friday).Subject;
        CalendarClashes.Find(calendars.DataFor(surgeon.Id, friday, friday), moved.StartUtc, moved.EndUtc, ignoreBlockId: moved.Id)
            .Should().BeNull("the Friday's admin day has its MDT meeting and admin time, and the lunch goes round them");
        CalendarProjection.TemplateBlocks(calendars.DataFor(surgeon.Id, friday, friday))
            .Should().NotContain(p => p.StartUtc == UkTime.ToUtc(friday, new TimeOnly(12, 0)), "the template's lunch is left off that day");
    }

    [TestCaseSource(nameof(Moments))]
    public void BookFreedSlot_BooksTheNursesFreedTime_AndReservesIt(DateTime nowUtc)
    {
        var timeline = new DemoTimeline(nowUtc);
        var staff = Staff(timeline);
        var world = new DemoWorld
        {
            Timeline = timeline,
            Random = new Random(Seed),
            Calendars = DemoCalendars.Plan(staff, timeline, new Random(Seed)),
            Staff = staff,
            Templates = new Dictionary<Pathway, PatientProcedureStageTemplate>(),
            Definitions = new Dictionary<string, PatientProcedureStageDefinition>(),
            Rules = [],
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
        var (nurse, start, end) = world.Calendars.FreedSlot!.Value;

        DemoProcedures.BookFreedSlot(world, patients);

        world.Appointments.Should().ContainSingle(a => a.StaffUserId == nurse && a.StartUtc == start && a.EndUtc == end);
        world.Allocator.IsFree(nurse, start, end).Should().BeFalse("a booked time is reserved, so no visit can be booked over it");
        world.Allocator.FreeStarts(nurse, UkTime.DateOf(start), 15, start.AddHours(-1))
            .Should().NotContain(s => CalendarClashes.Overlaps(s, s.AddMinutes(15), start, end));
    }
}
