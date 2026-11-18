using FluentAssertions;
using MedApp.UnitTests.Common.Fixtures;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.StaffCalendars.Commands.CreateCalendarBlock;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Calendar.StaffCalendars.Commands.CreateCalendarBlock;

[TestFixture]
public sealed class CreateCalendarBlockValidatorTests
{
    private readonly CreateCalendarBlockValidator _validator = new(InMemoryClosedDays.WorkingDays(CreateClosedDay()));

    private static CreateCalendarBlockCommand Valid() =>
        new(StaffUserId, Training.Id, At(NextMonday, 10), At(NextMonday, 11), BlockNote, StaffActor);

    [Test]
    public void Validate_ValidBlock_Passes()
    {
        _validator.ValidateNow(Valid()).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_WholeWorkingDayWithoutANote_Passes()
    {
        _validator.ValidateNow(Valid() with { StartUtc = At(NextMonday, 8), EndUtc = At(NextMonday, 18), Note = null })
            .IsValid.Should().BeTrue();
    }

    [TestCaseSource(typeof(BlockTimeCases), nameof(BlockTimeCases.ValidInSummer))]
    public void Validate(DateTime start, DateTime end)
    {
        _validator.ValidateNow(Valid() with { StartUtc = start, EndUtc = end }).IsValid.Should().BeTrue();
    }

    [TestCaseSource(typeof(BlockTimeCases), nameof(BlockTimeCases.OutsideTheDefaultDay))]
    public void ValidateOutsideTheirDay(DateTime start, DateTime end, string message)
    {
        // Their working day is the default's (they have no hours of their own).
        _validator.ValidateNow(Valid() with { StartUtc = start, EndUtc = end })
            .Errors.Should().ContainSingle().Which.ErrorMessage.Should().Be(message);
    }

    [TestCaseSource(typeof(BlockTimeCases), nameof(BlockTimeCases.Invalid))]
    public void Validate(DateTime start, DateTime end, string message)
    {
        var result = _validator.ValidateNow(Valid() with { StartUtc = start, EndUtc = end });

        result.Errors.Should().ContainSingle().Which.ErrorMessage.Should().Be(message);
    }

    [Test]
    public void Validate_NoMemberOfStaff_Fails()
    {
        _validator.ValidateNow(Valid() with { StaffUserId = Guid.Empty })
            .Errors.Should().ContainSingle().Which.ErrorMessage.Should().Be("Member of staff is required.");
    }

    [Test]
    public void Validate_NoActivityType_Fails()
    {
        _validator.ValidateNow(Valid() with { ActivityTypeId = Guid.Empty })
            .Errors.Should().ContainSingle().Which.ErrorMessage.Should().Be("Activity type is required.");
    }

    [Test]
    public void Validate_NoteAtTheLimit_Passes()
    {
        _validator.ValidateNow(Valid() with { Note = new string('a', CalendarLimits.MaxNoteLength) }).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_NoteTooLong_Fails()
    {
        _validator.ValidateNow(Valid() with { Note = new string('a', CalendarLimits.MaxNoteLength + 1) })
            .Errors.Should().ContainSingle().Which.ErrorMessage.Should().Be("Note must not exceed 200 characters.");
    }

    [Test]
    public void Validate_OnAClosedDay_SaysTheClinicIsClosed()
    {
        _validator.ValidateNow(Valid() with { StartUtc = At(ClosedWednesday, 10), EndUtc = At(ClosedWednesday, 11) })
            .Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be(WorkingHours.ClosedDay(ClosedWednesday, ClosedDayName));
    }
}
