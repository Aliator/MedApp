using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.StaffCalendars.Commands.UpdateCalendarBlock;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Calendar.StaffCalendars.Commands.UpdateCalendarBlock;

[TestFixture]
public sealed class UpdateCalendarBlockValidatorTests
{
    private readonly UpdateCalendarBlockValidator _validator = new();

    private static UpdateCalendarBlockCommand Valid() =>
        new(Guid.NewGuid(), Training.Id, At(NextMonday, 10), At(NextMonday, 11), BlockNote, StaffActor);

    [Test]
    public void Validate_ValidBlock_Passes()
    {
        _validator.Validate(Valid()).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_WholeWorkingDayWithoutANote_Passes()
    {
        _validator.Validate(Valid() with { StartUtc = At(NextMonday, 8), EndUtc = At(NextMonday, 18), Note = null })
            .IsValid.Should().BeTrue();
    }

    [TestCaseSource(typeof(BlockTimeCases), nameof(BlockTimeCases.ValidInSummer))]
    public void Validate(DateTime start, DateTime end)
    {
        _validator.Validate(Valid() with { StartUtc = start, EndUtc = end }).IsValid.Should().BeTrue();
    }

    [TestCaseSource(typeof(BlockTimeCases), nameof(BlockTimeCases.Invalid))]
    public void Validate(DateTime start, DateTime end, string message)
    {
        var result = _validator.Validate(Valid() with { StartUtc = start, EndUtc = end });

        result.Errors.Should().ContainSingle().Which.ErrorMessage.Should().Be(message);
    }

    [Test]
    public void Validate_NoBlockId_Fails()
    {
        _validator.Validate(Valid() with { BlockId = Guid.Empty })
            .Errors.Should().ContainSingle().Which.ErrorMessage.Should().Be("Block id is required.");
    }

    [Test]
    public void Validate_NoActivityType_Fails()
    {
        _validator.Validate(Valid() with { ActivityTypeId = Guid.Empty })
            .Errors.Should().ContainSingle().Which.ErrorMessage.Should().Be("Activity type is required.");
    }

    [Test]
    public void Validate_NoteAtTheLimit_Passes()
    {
        _validator.Validate(Valid() with { Note = new string('a', CalendarLimits.MaxNoteLength) }).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_NoteTooLong_Fails()
    {
        _validator.Validate(Valid() with { Note = new string('a', CalendarLimits.MaxNoteLength + 1) })
            .Errors.Should().ContainSingle().Which.ErrorMessage.Should().Be("Note must not exceed 200 characters.");
    }
}
