using FluentAssertions;
using FluentValidation.Results;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.DayTemplates.Commands.CreateDayTemplate;
using static MedApp.UnitTests.Common.Constants.CalendarSetupTestConstants;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Calendar.Common;

/// <summary>The rules every day template command shares (<see cref="DayTemplateValidation{T}"/>), through the create command.</summary>
[TestFixture]
public sealed class DayTemplateValidationTests
{
    private readonly CreateDayTemplateValidator _validator = new();

    private ValidationResult Validate(params DayTemplateBlockValue[] blocks) =>
        _validator.Validate(new CreateDayTemplateCommand(StaffUserId, DayTemplateName, blocks, StaffActor));

    [Test]
    public void Validate_NoBlocks_Passes()
    {
        Validate().IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_BlocksFillingTheWorkingDayEndToStart_Passes()
    {
        Validate(BlockValue(8, 12), BlockValue(13, 18, Training.Id), BlockValue(12, 13, note: TemplateNote))
            .IsValid.Should().BeTrue();
    }

    [TestCase("")]
    [TestCase(" ")]
    public void Validate_BlankName_Fails(string name)
    {
        var result = _validator.Validate(new CreateDayTemplateCommand(StaffUserId, name, [], StaffActor));

        result.Errors.Should().ContainSingle(e => e.PropertyName == "Name" && e.ErrorMessage == "Name is required.");
    }

    [Test]
    public void Validate_NameTooLong_Fails()
    {
        var result = _validator.Validate(new CreateDayTemplateCommand(StaffUserId, new string('n', CalendarLimits.MaxTemplateNameLength + 1), [], StaffActor));

        result.Errors.Should().ContainSingle(e => e.PropertyName == "Name" && e.ErrorMessage.Contains("must not exceed"));
    }

    [Test]
    public void Validate_NameAtTheLimit_Passes()
    {
        _validator.Validate(new CreateDayTemplateCommand(StaffUserId, new string('n', CalendarLimits.MaxTemplateNameLength), [], StaffActor))
            .IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_NullBlocks_Fails()
    {
        var result = _validator.Validate(new CreateDayTemplateCommand(StaffUserId, DayTemplateName, null!, StaffActor));

        result.Errors.Should().ContainSingle(e => e.ErrorMessage == "Blocks must not be null.");
    }

    [Test]
    public void Validate_MostBlocksAllowed_Passes()
    {
        var blocks = Enumerable.Range(0, CalendarLimits.MaxDayTemplateBlocks)
            .Select(i => BlockValue(8, 8, startMinute: i, endMinute: i + 1))
            .ToArray();

        // Minutes aren't on 5-minute boundaries; only the count is under test.
        Validate(blocks).Errors.Should().NotContain(e => e.ErrorMessage.Contains("at most"));
    }

    [Test]
    public void Validate_TooManyBlocks_Fails()
    {
        var blocks = Enumerable.Range(0, CalendarLimits.MaxDayTemplateBlocks + 1)
            .Select(i => BlockValue(8 + i / 6, 8 + i / 6, startMinute: i % 6 * 5, endMinute: i % 6 * 5 + 5))
            .ToArray();

        var result = Validate(blocks);

        result.Errors.Should().ContainSingle(e => e.ErrorMessage == $"A day template can have at most {CalendarLimits.MaxDayTemplateBlocks} blocks.");
    }

    [Test]
    public void Validate_BlockWithoutActivityType_Fails()
    {
        Validate(BlockValue(12, 13, Guid.Empty)).Errors.Should().ContainSingle(e => e.ErrorMessage == "Activity type is required.");
    }

    [TestCase(5, 0, 9, 0, WorkingHours.OutsideBounds)]
    [TestCase(17, 0, 23, 30, WorkingHours.OutsideBounds)]
    [TestCase(13, 0, 12, 0, WorkingHours.EndNotAfterStart)]
    [TestCase(12, 0, 12, 0, WorkingHours.EndNotAfterStart)]
    [TestCase(12, 2, 13, 0, WorkingHours.NotOnBoundary)]
    [TestCase(12, 0, 12, 58, WorkingHours.NotOnBoundary)]
    public void Validate_BlockOutsideTheWorkingDayOrOffTheGrid_FailsSayingWhy(int startHour, int startMinute, int endHour, int endMinute, string why)
    {
        var result = Validate(BlockValue(startHour, endHour, startMinute: startMinute, endMinute: endMinute));

        result.Errors.Should().ContainSingle(e => e.ErrorMessage == why && e.PropertyName.EndsWith("StartTime"));
    }

    [Test]
    public void Validate_NoteAtTheLimit_Passes()
    {
        Validate(BlockValue(12, 13, note: new string('x', CalendarLimits.MaxNoteLength))).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_NoteTooLong_Fails()
    {
        var result = Validate(BlockValue(12, 13, note: new string('x', CalendarLimits.MaxNoteLength + 1)));

        result.Errors.Should().ContainSingle(e => e.ErrorMessage == $"Note must not exceed {CalendarLimits.MaxNoteLength} characters.");
    }

    [Test]
    public void Validate_OverlappingBlocks_FailsNamingBoth()
    {
        var result = Validate(BlockValue(14, 16, Training.Id), BlockValue(12, 13), BlockValue(15, 17));

        result.Errors.Should().ContainSingle(e => e.ErrorMessage == "Blocks can't overlap: 14:00 to 16:00 overlaps 15:00 to 17:00.");
    }

    [Test]
    public void Validate_TwoBlocksAtTheSameTime_Fails()
    {
        Validate(BlockValue(12, 13), BlockValue(12, 13, Training.Id))
            .Errors.Should().ContainSingle(e => e.ErrorMessage.StartsWith("Blocks can't overlap"));
    }

    [Test]
    public void Validate_SeveralOverlaps_ReportsOnlyTheFirst()
    {
        Validate(BlockValue(9, 11), BlockValue(10, 12), BlockValue(14, 16), BlockValue(15, 17))
            .Errors.Should().ContainSingle(e => e.ErrorMessage.StartsWith("Blocks can't overlap: 09:00 to 11:00"));
    }
}
