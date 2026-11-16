using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.StaffCalendars.Commands.ChangeTemplateBlockForDay;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Calendar.StaffCalendars.Commands.ChangeTemplateBlockForDay;

[TestFixture]
public sealed class ChangeTemplateBlockForDayValidatorTests
{
    private readonly ChangeTemplateBlockForDayValidator _validator = new();

    private static ChangeTemplateBlockForDayCommand Valid() =>
        new(StaffUserId, NextMonday, Guid.NewGuid(), Lunch.Id, At(NextMonday, 12, 30), At(NextMonday, 13, 30), BlockNote, StaffActor);

    [Test]
    public void Validate_ValidChange_Passes()
    {
        _validator.Validate(Valid()).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_NoNote_Passes()
    {
        _validator.Validate(Valid() with { Note = null }).IsValid.Should().BeTrue();
    }

    [TestCaseSource(typeof(BlockTimeCases), nameof(BlockTimeCases.Invalid))]
    public void Validate(DateTime start, DateTime end, string message)
    {
        var result = _validator.Validate(Valid() with { StartUtc = start, EndUtc = end });

        result.Errors.Should().ContainSingle().Which.ErrorMessage.Should().Be(message);
    }

    [Test]
    public void Validate_MovedToAnotherDay_Fails()
    {
        var result = _validator.Validate(Valid() with { StartUtc = At(NextFriday, 12), EndUtc = At(NextFriday, 13) });

        result.Errors.Should().ContainSingle().Which.ErrorMessage.Should().Be(ChangeTemplateBlockForDayValidator.SameDay);
    }

    [Test]
    public void Validate_SummerDayAtUkTimes_Passes()
    {
        _validator.Validate(Valid() with { Date = SummerMonday, StartUtc = At(SummerMonday, 8), EndUtc = At(SummerMonday, 9) })
            .IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_NoMemberOfStaff_Fails()
    {
        _validator.Validate(Valid() with { StaffUserId = Guid.Empty })
            .Errors.Should().ContainSingle().Which.ErrorMessage.Should().Be("Member of staff is required.");
    }

    [Test]
    public void Validate_NoTemplateBlock_Fails()
    {
        _validator.Validate(Valid() with { TemplateBlockId = Guid.Empty })
            .Errors.Should().ContainSingle().Which.ErrorMessage.Should().Be("Template block id is required.");
    }

    [Test]
    public void Validate_NoActivityType_Fails()
    {
        _validator.Validate(Valid() with { ActivityTypeId = Guid.Empty })
            .Errors.Should().ContainSingle().Which.ErrorMessage.Should().Be("Activity type is required.");
    }

    [Test]
    public void Validate_NoteTooLong_Fails()
    {
        _validator.Validate(Valid() with { Note = new string('a', CalendarLimits.MaxNoteLength + 1) })
            .Errors.Should().ContainSingle().Which.ErrorMessage.Should().Be("Note must not exceed 200 characters.");
    }
}
