using FluentAssertions;
using MedApp.Application.Calendar.Common;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Calendar.Common;

[TestFixture]
public sealed class CalendarProjectionTests
{
    [Test]
    public void Days_Week_MarksWeekendsPastDaysAndToday()
    {
        var data = CreateData(Today.AddDays(-2), Today.AddDays(4));

        var days = CalendarProjection.Days(data, Today);

        days.Select(d => d.Date).Should().Equal(Enumerable.Range(-2, 7).Select(i => Today.AddDays(i)));
        days.Select(d => d.IsWorkingDay).Should().Equal(false, false, true, true, true, true, true);
        days.Select(d => d.IsPast).Should().Equal(true, true, false, false, false, false, false);
        days.Select(d => d.IsToday).Should().Equal(false, false, true, false, false, false, false);
    }

    [Test]
    public void Days_EachDay_GivesItsWorkingDayInUtc()
    {
        var winter = CalendarProjection.Days(CreateData(NextMonday), Today).Single();
        var summer = CalendarProjection.Days(CreateData(SummerMonday), Today).Single();

        winter.WorkStartUtc.Should().Be(new DateTime(2026, 11, 23, 8, 0, 0, DateTimeKind.Utc));
        winter.WorkEndUtc.Should().Be(new DateTime(2026, 11, 23, 18, 0, 0, DateTimeKind.Utc));
        summer.WorkStartUtc.Should().Be(new DateTime(2026, 6, 15, 7, 0, 0, DateTimeKind.Utc));
        summer.WorkEndUtc.Should().Be(new DateTime(2026, 6, 15, 17, 0, 0, DateTimeKind.Utc));
    }

    [Test]
    public void Days_DefaultWeek_GivesEachWeekdayItsDayTemplate()
    {
        var days = CalendarProjection.Days(CreateData(NextMonday, NextSunday), Today);

        days.Take(5).Should().AllSatisfy(d =>
        {
            d.DayTemplateId.Should().Be(DayTemplateId);
            d.DayTemplateName.Should().Be(DayTemplateName);
            d.IsDayTemplateOverridden.Should().BeFalse();
        });
        days.Skip(5).Should().AllSatisfy(d => d.DayTemplateId.Should().BeNull());
    }

    [Test]
    public void Days_DayOverride_WinsOverTheWeek()
    {
        var data = CreateData(NextMonday, NextMonday.AddDays(1), overrides:
        [
            CreateOverride(NextMonday, OtherDayTemplateId),
            CreateOverride(NextMonday.AddDays(1), null),
        ]);

        var days = CalendarProjection.Days(data, Today);

        days[0].Should().Match<CalendarDay>(d =>
            d.DayTemplateId == OtherDayTemplateId && d.DayTemplateName == OtherDayTemplateName && d.IsDayTemplateOverridden);
        days[1].Should().Match<CalendarDay>(d => d.DayTemplateId == null && d.DayTemplateName == null && d.IsDayTemplateOverridden);
    }

    [Test]
    public void Days_NoDefaultWeek_HaveNoDayTemplate()
    {
        var day = CalendarProjection.Days(CreateData(NextMonday, staffWeeks: []), Today).Single();

        day.DayTemplateId.Should().BeNull();
        day.IsDayTemplateOverridden.Should().BeFalse();
    }

    [Test]
    public void TemplateBlocks_DefaultWeek_ProjectsTheBlocksOnWeekdaysOnly()
    {
        var lunch = CreateTemplateBlock(12, 13);

        var blocks = CalendarProjection.TemplateBlocks(CreateData(NextMonday, NextSunday, [lunch]));

        blocks.Select(b => b.Date).Should().Equal(Enumerable.Range(0, 5).Select(i => NextMonday.AddDays(i)));
        blocks.Should().AllSatisfy(b => b.Source.Should().BeSameAs(lunch));
        blocks[0].StartUtc.Should().Be(new DateTime(2026, 11, 23, 12, 0, 0, DateTimeKind.Utc));
        blocks[0].EndUtc.Should().Be(new DateTime(2026, 11, 23, 13, 0, 0, DateTimeKind.Utc));
    }

    [Test]
    public void TemplateBlocks_WeekSpanningTheClockChange_KeepsTheUkClockTime()
    {
        var lunch = CreateTemplateBlock(12, 13);

        var blocks = CalendarProjection.TemplateBlocks(CreateData(FridayBeforeClockChange, MondayAfterClockChange, [lunch]));

        blocks.Select(b => (b.Date, b.StartUtc)).Should().Equal(
            (FridayBeforeClockChange, new DateTime(2026, 10, 23, 11, 0, 0, DateTimeKind.Utc)),
            (MondayAfterClockChange, new DateTime(2026, 10, 26, 12, 0, 0, DateTimeKind.Utc)));
    }

    [Test]
    public void TemplateBlocks_BlocksOfADay_AreInStartOrder()
    {
        var lunch = CreateTemplateBlock(12, 13);
        var training = CreateTemplateBlock(9, 10, Training);

        var blocks = CalendarProjection.TemplateBlocks(CreateData(NextMonday, templateBlocks: [lunch, training]));

        blocks.Select(b => b.Source).Should().Equal(training, lunch);
    }

    [Test]
    public void TemplateBlocks_Exception_LeavesThatBlockOffThatDayOnly()
    {
        var lunch = CreateTemplateBlock(12, 13);
        var training = CreateTemplateBlock(9, 10, Training);

        var blocks = CalendarProjection.TemplateBlocks(CreateData(
            NextMonday,
            NextMonday.AddDays(1),
            [lunch, training],
            exceptions: [CreateException(NextMonday, lunch)]));

        blocks.Select(b => (b.Date, b.Source)).Should().Equal(
            (NextMonday, training),
            (NextMonday.AddDays(1), training),
            (NextMonday.AddDays(1), lunch));
    }

    [Test]
    public void TemplateBlocks_DayOverride_ProjectsItsDayTemplatesBlocksInstead()
    {
        var clinicLunch = CreateTemplateBlock(12, 13);
        var theatreLunch = CreateTemplateBlock(13, 14, dayTemplateId: OtherDayTemplateId);

        var blocks = CalendarProjection.TemplateBlocks(CreateData(
            NextMonday,
            templateBlocks: [clinicLunch, theatreLunch],
            overrides: [CreateOverride(NextMonday, OtherDayTemplateId)]));

        blocks.Should().ContainSingle().Which.Source.Should().BeSameAs(theatreLunch);
    }

    [Test]
    public void TemplateBlocks_DayOverrideWithNone_ProjectsNothing()
    {
        var blocks = CalendarProjection.TemplateBlocks(CreateData(
            NextMonday,
            templateBlocks: [CreateTemplateBlock(12, 13)],
            overrides: [CreateOverride(NextMonday, null)]));

        blocks.Should().BeEmpty();
    }

    [Test]
    public void TemplateBlocks_PastDays_UseTheRowsValidThen()
    {
        // Lunch moved from 12:00 to 12:30 today; the default week changed from the clinic week to the theatre week
        // today as well. Friday 13 November keeps what applied then.
        var lastFriday = Today.AddDays(-3);
        var oldLunch = CreateTemplateBlock(12, 13, validTo: Today.AddDays(-1));
        var newLunch = CreateTemplateBlock(12, 13, validFrom: Today, startMinute: 30, endMinute: 30);
        var theatreBlock = CreateTemplateBlock(14, 15, Training, OtherDayTemplateId, validFrom: LongAgo);

        var data = CreateData(
            lastFriday,
            Today,
            [oldLunch, newLunch, theatreBlock],
            staffWeeks:
            [
                CreateStaffWeek(WeekTemplateId, validTo: Today.AddDays(-1)),
                CreateStaffWeek(OtherWeekTemplateId, validFrom: Today),
            ],
            weekDays: CreateWorkingWeek().Concat(CreateWorkingWeek(OtherDayTemplateId, OtherWeekTemplateId)));

        var blocks = CalendarProjection.TemplateBlocks(data);

        blocks.Select(b => (b.Date, b.Source)).Should().Equal((lastFriday, oldLunch), (Today, theatreBlock));
        CalendarProjection.Days(data, Today).Select(d => d.DayTemplateId)
            .Should().Equal(DayTemplateId, null, null, OtherDayTemplateId);
    }

    [Test]
    public void TemplateBlocks_DayTemplateBlockEditedToday_ShowsTheNewVersionFromToday()
    {
        var oldLunch = CreateTemplateBlock(12, 13, validTo: Today.AddDays(-1));
        var newLunch = CreateTemplateBlock(12, 13, validFrom: Today, startMinute: 30, endMinute: 30);

        var blocks = CalendarProjection.TemplateBlocks(CreateData(Today, Today.AddDays(1), [oldLunch, newLunch]));

        blocks.Should().AllSatisfy(b => b.Source.Should().BeSameAs(newLunch));
        blocks.Should().HaveCount(2);
    }

    [Test]
    public void TemplateBlocks_WeekdayRowNotValidOnTheDate_ProjectsNothing()
    {
        var weekDays = CalendarSetupMapper.Weekdays
            .Select(d => CreateWeekDay(d, validFrom: NextMonday.AddDays(1)))
            .ToList();

        var blocks = CalendarProjection.TemplateBlocks(CreateData(NextMonday, templateBlocks: [CreateTemplateBlock(12, 13)], weekDays: weekDays));

        blocks.Should().BeEmpty();
    }

    [Test]
    public void TemplateBlocks_NoDefaultWeek_ProjectsNothing()
    {
        var blocks = CalendarProjection.TemplateBlocks(CreateData(NextMonday, templateBlocks: [CreateTemplateBlock(12, 13)], staffWeeks: []));

        blocks.Should().BeEmpty();
    }

    [Test]
    public void TemplateBlocks_SomeoneElsesDefaultWeekOrException_AreIgnored()
    {
        var lunch = CreateTemplateBlock(12, 13);
        var othersException = CreateException(NextMonday, lunch);
        othersException.StaffUserId = OtherStaffUserId;

        var blocks = CalendarProjection.TemplateBlocks(CreateData(
            NextMonday,
            templateBlocks: [lunch],
            staffWeeks: [CreateStaffWeek(staffUserId: OtherStaffUserId), CreateStaffWeek()],
            exceptions: [othersException]));

        blocks.Should().ContainSingle().Which.Source.Should().BeSameAs(lunch);
    }

    [Test]
    public void DayTemplateOn_Weekend_IsNoneEvenWithAnOverride()
    {
        var data = CreateData(NextSaturday, overrides: [CreateOverride(NextSaturday, DayTemplateId)]);

        CalendarProjection.DayTemplateOn(data, NextSaturday).Should().Be(((Guid?)null, false));
    }

    [Test]
    public void DefaultWeekTemplateOn_Date_IsTheRowValidThen()
    {
        var old = CreateStaffWeek(WeekTemplateId, validTo: Today.AddDays(-1));
        var current = CreateStaffWeek(OtherWeekTemplateId, validFrom: Today);
        var data = CreateData(Today.AddDays(-1), Today, staffWeeks: [old, current]);

        CalendarProjection.DefaultWeekTemplateOn(data, Today.AddDays(-1)).Should().BeSameAs(old);
        CalendarProjection.DefaultWeekTemplateOn(data, Today).Should().BeSameAs(current);
    }
}
