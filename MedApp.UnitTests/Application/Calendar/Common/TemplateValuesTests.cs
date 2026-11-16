using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.WeekTemplates.Commands.CreateWeekTemplate;
using static MedApp.UnitTests.Common.Constants.CalendarSetupTestConstants;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Calendar.Common;

/// <summary>The values templates are asked for with: <see cref="DayTemplateBlockValue"/> and <see cref="WeekTemplateDayValue"/>.</summary>
[TestFixture]
public sealed class TemplateValuesTests
{
    [TestCase(null, "")]
    [TestCase("", "")]
    [TestCase("   ", "")]
    [TestCase($"  {TemplateNote} ", TemplateNote)]
    public void NoteText_IsTheNoteTrimmed(string? note, string expected)
    {
        BlockValue(12, 13, note: note).NoteText.Should().Be(expected);
    }

    [Test]
    public void Normalised_EqualsTheStoredBlocksValue()
    {
        var stored = CreateTemplateBlock(12, 13, note: TemplateNote);

        BlockValue(12, 13, note: $" {TemplateNote}  ").Normalised.Should().Be(DayTemplateBlockValue.Of(stored));
        BlockValue(12, 13, note: TemplateNote).Should().NotBe(BlockValue(12, 13, note: $" {TemplateNote}"));
    }

    [Test]
    public void Of_TakesTheBlocksActivityTimesAndNote()
    {
        var block = CreateTemplateBlock(14, 15, Training, startMinute: 30, note: TemplateNote);

        DayTemplateBlockValue.Of(block).Should().Be(
            new DayTemplateBlockValue(Training.Id, new TimeOnly(14, 30), new TimeOnly(15, 0), TemplateNote));
    }

    [Test]
    public void ToBlock_MakesANewRowValidFromTheDateWithTheNoteTrimmed()
    {
        var value = BlockValue(12, 13, Training.Id, $" {TemplateNote} ", endMinute: 30);

        var block = value.ToBlock(DayTemplateId, Today);
        var another = value.ToBlock(DayTemplateId, Today);

        block.Id.Should().NotBeEmpty().And.NotBe(another.Id);
        block.DayTemplateId.Should().Be(DayTemplateId);
        block.ActivityTypeId.Should().Be(Training.Id);
        block.StartTime.Should().Be(new TimeOnly(12, 0));
        block.EndTime.Should().Be(new TimeOnly(13, 30));
        block.Note.Should().Be(TemplateNote);
        block.ValidFrom.Should().Be(Today);
        block.ValidTo.Should().BeNull();
    }

    [Test]
    public void From_KeepsOnlyTheWeekdaysWithADayTemplateInOrder()
    {
        WeekTemplateDayValue.From(DayTemplateId, null, OtherDayTemplateId, null, DayTemplateId).Should().Equal(
            new WeekTemplateDayValue(DayOfWeek.Monday, DayTemplateId),
            new WeekTemplateDayValue(DayOfWeek.Wednesday, OtherDayTemplateId),
            new WeekTemplateDayValue(DayOfWeek.Friday, DayTemplateId));
    }

    [Test]
    public void From_NoneChosen_IsEmpty()
    {
        WeekTemplateDayValue.From(null, null, null, null, null).Should().BeEmpty();
    }

    [Test]
    public void From_EveryDay_GivesMondayToFriday()
    {
        WeekTemplateDayValue.From(DayTemplateId, DayTemplateId, DayTemplateId, DayTemplateId, OtherDayTemplateId)
            .Select(d => d.DayOfWeek).Should().Equal(CalendarSetupMapper.Weekdays);
    }

    [Test]
    public void Days_TakesTheCommandsWeekdays()
    {
        var command = new CreateWeekTemplateCommand(WeekTemplateName, null, OtherDayTemplateId, null, DayTemplateId, null);

        command.Days().Should().Equal(
            new WeekTemplateDayValue(DayOfWeek.Tuesday, OtherDayTemplateId),
            new WeekTemplateDayValue(DayOfWeek.Thursday, DayTemplateId));
    }

    [Test]
    public void Of_TakesTheDaysWeekdayAndDayTemplate()
    {
        WeekTemplateDayValue.Of(CreateWeekDay(DayOfWeek.Thursday, OtherDayTemplateId))
            .Should().Be(new WeekTemplateDayValue(DayOfWeek.Thursday, OtherDayTemplateId));
    }

    [Test]
    public void ToDay_MakesANewRowValidFromTheDate()
    {
        var day = new WeekTemplateDayValue(DayOfWeek.Wednesday, OtherDayTemplateId).ToDay(WeekTemplateId, Today);

        day.Id.Should().NotBeEmpty();
        day.WeekTemplateId.Should().Be(WeekTemplateId);
        day.DayOfWeek.Should().Be(DayOfWeek.Wednesday);
        day.DayTemplateId.Should().Be(OtherDayTemplateId);
        day.ValidFrom.Should().Be(Today);
        day.ValidTo.Should().BeNull();
    }
}
