using FluentAssertions;
using MedApp.Application.Appointments.Commands.RecordPastAppointment;
using MedApp.Application.Appointments.Common;
using MedApp.Application.Calendar.Common;
using MedApp.UnitTests.Common.Fixtures;
using static MedApp.UnitTests.Common.Constants.AppointmentsTestConstants;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Appointments.Commands.RecordPastAppointment;

[TestFixture]
public sealed class RecordPastAppointmentValidatorTests
{
    /// <summary>Thursday 12 November 2026, four days before the clock's Monday.</summary>
    private static readonly DateOnly LastThursday = new(2026, 11, 12);

    /// <summary>Tuesday 10 November 2026: a weekday, closed or not, it makes no difference here.</summary>
    private static readonly DateOnly ClosedTuesday = new(2026, 11, 10);

    private readonly RecordPastAppointmentValidator _validator =
        new(new FixedTimeProvider(Now));

    private static RecordPastAppointmentCommand Valid() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), StaffUserId, At(LastThursday, 10), At(LastThursday, 10, 30),
            "Attended", Notes, Colleague);

    private IEnumerable<string> Errors(RecordPastAppointmentCommand command) =>
        _validator.ValidateNow(command).Errors.Select(e => e.ErrorMessage);

    [Test]
    public void Validate_Valid_Passes()
    {
        _validator.ValidateNow(Valid()).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_NoPathwayStageOrNotes_Passes()
    {
        _validator.ValidateNow(Valid() with { PathwayId = null, StageId = null, Notes = null }).IsValid.Should().BeTrue();
    }

    [TestCase("Attended")]
    [TestCase("didnotattend")]
    public void Validate_AnOutcome_Passes(string outcome)
    {
        _validator.ValidateNow(Valid() with { Outcome = outcome }).IsValid.Should().BeTrue();
    }

    [TestCase("Booked")]
    [TestCase("Requested")]
    [TestCase("Cancelled")]
    [TestCase("")]
    public void Validate_NotAnOutcome_Fails(string outcome)
    {
        Errors(Valid() with { Outcome = outcome }).Should().Equal("Outcome must be one of: Attended, DidNotAttend.");
    }

    [Test]
    public void Validate_NoPatient_Fails()
    {
        Errors(Valid() with { PatientId = Guid.Empty }).Should().Equal("Patient is required.");
    }

    [Test]
    public void Validate_StageWithoutItsPathway_Fails()
    {
        Errors(Valid() with { PathwayId = null }).Should().Equal("A stage needs its pathway.");
    }

    [Test]
    public void Validate_NoMemberOfStaff_Fails()
    {
        Errors(Valid() with { StaffUserId = Guid.Empty }).Should().Equal("Member of staff is required.");
    }

    [Test]
    public void Validate_NotesTooLong_Fails()
    {
        Errors(Valid() with { Notes = new string('a', AppointmentLimits.MaxNotesLength + 1) })
            .Should().Equal($"Notes must not exceed {AppointmentLimits.MaxNotesLength} characters.");
    }

    [Test]
    public void Validate_LaterToday_HasNotStarted()
    {
        Errors(Valid() with { StartUtc = At(Today, 10), EndUtc = At(Today, 10, 30) }).Should().Equal(BookingTimes.NotStartedYet);
    }

    [Test]
    public void Validate_MoreThanThirtyDaysAgo_Fails()
    {
        var longAgo = new DateOnly(2026, 10, 15);

        Errors(Valid() with { StartUtc = At(longAgo, 10), EndUtc = At(longAgo, 10, 30) }).Should().Equal(BookingTimes.TooLongAgo);
    }

    [Test]
    public void Validate_OnADaySinceClosed_Passes()
    {
        // The validator doesn't look closed days up at all: a walk-in may be on one (decision 74).
        _validator.ValidateNow(Valid() with { StartUtc = At(ClosedTuesday, 10), EndUtc = At(ClosedTuesday, 10, 30) })
            .IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_Weekend_StillFails()
    {
        var saturday = new DateOnly(2026, 11, 14);

        Errors(Valid() with { StartUtc = At(saturday, 10), EndUtc = At(saturday, 10, 30) }).Should().Equal(WorkingHours.Weekend);
    }

    [Test]
    public void Validate_OutsideTheWorkingDay_Fails()
    {
        Errors(Valid() with { StartUtc = At(LastThursday, 18), EndUtc = At(LastThursday, 18, 30) })
            .Should().Equal(WorkingHours.OutsideWorkingDay);
    }

    [Test]
    public void Validate_NotOnAFiveMinuteBoundary_Fails()
    {
        Errors(Valid() with { StartUtc = At(LastThursday, 10, 2) }).Should().Equal(WorkingHours.NotOnBoundary);
    }
}
