using FluentAssertions;
using MedApp.Application.Appointments.Commands.BookAppointment;
using MedApp.Application.Appointments.Common;
using MedApp.Application.Calendar.Common;
using MedApp.UnitTests.Common.Fixtures;
using static MedApp.UnitTests.Common.Constants.AppointmentsTestConstants;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Appointments.Commands.BookAppointment;

[TestFixture]
public sealed class BookAppointmentValidatorTests
{
    private readonly BookAppointmentValidator _validator = new(new FixedTimeProvider(Now));

    private static BookAppointmentCommand Valid() =>
        new(Guid.NewGuid(), StaffUserId, At(NextMonday, 10), At(NextMonday, 10, 30), "Requested", Colleague);

    private IEnumerable<string> Errors(BookAppointmentCommand command) =>
        _validator.Validate(command).Errors.Select(e => e.ErrorMessage);

    [Test]
    public void Validate_FutureWorkingTime_Passes()
    {
        _validator.Validate(Valid()).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_NoAppointment_Fails()
    {
        Errors(Valid() with { AppointmentId = Guid.Empty }).Should().Equal("Appointment id is required.");
    }

    [Test]
    public void Validate_NoStaffMember_Fails()
    {
        Errors(Valid() with { StaffUserId = Guid.Empty }).Should().Equal("Member of staff is required.");
    }

    [TestCase("Requested")]
    [TestCase("Booked")]
    [TestCase("booked")]
    public void Validate_ExpectedStatusOpen_Passes(string expected)
    {
        _validator.Validate(Valid() with { ExpectedStatus = expected }).IsValid.Should().BeTrue();
    }

    [TestCase("")]
    [TestCase(" ")]
    [TestCase(null)]
    [TestCase("Attended")]
    [TestCase("Cancelled")]
    [TestCase("1")]
    [TestCase("Rescheduled")]
    public void Validate_ExpectedStatusNotRequestedOrBooked_Fails(string? expected)
    {
        Errors(Valid() with { ExpectedStatus = expected! }).Should().Equal(BookAppointmentValidator.ExpectedStatusMessage);
    }

    [Test]
    public void Validate_InThePast_Fails()
    {
        // 08:00 today, an hour before the fixed clock's 09:00.
        Errors(Valid() with { StartUtc = At(Today, 8), EndUtc = At(Today, 8, 30) }).Should().Equal(BookingTimes.InThePast);
    }

    [Test]
    public void Validate_AtTheWeekend_Fails()
    {
        Errors(Valid() with { StartUtc = At(NextSaturday, 10), EndUtc = At(NextSaturday, 11) }).Should().Equal(WorkingHours.Weekend);
    }

    [Test]
    public void Validate_OutsideTheWorkingDay_Fails()
    {
        Errors(Valid() with { StartUtc = At(NextMonday, 17, 30), EndUtc = At(NextMonday, 18, 30) })
            .Should().Equal(WorkingHours.OutsideWorkingDay);
    }

    [Test]
    public void Validate_EndBeforeStart_Fails()
    {
        Errors(Valid() with { StartUtc = At(NextMonday, 11), EndUtc = At(NextMonday, 10) }).Should().Equal(WorkingHours.EndNotAfterStart);
    }

    [Test]
    public void Validate_NotOnAFiveMinuteBoundary_Fails()
    {
        Errors(Valid() with { StartUtc = At(NextMonday, 10, 2) }).Should().Equal(WorkingHours.NotOnBoundary);
    }
}
