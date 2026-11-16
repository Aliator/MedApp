using FluentAssertions;
using MedApp.Application.Appointments.Commands.CreateAppointment;
using MedApp.Application.Appointments.Common;
using MedApp.Application.Calendar.Common;
using MedApp.UnitTests.Common.Fixtures;
using static MedApp.UnitTests.Common.Constants.AppointmentsTestConstants;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Appointments.Commands.CreateAppointment;

[TestFixture]
public sealed class CreateAppointmentValidatorTests
{
    private readonly CreateAppointmentValidator _validator = new(new FixedTimeProvider(Now));

    private static CreateAppointmentCommand Request() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, null, null, Notes, Colleague);

    private static CreateAppointmentCommand Booking() =>
        Request() with { StaffUserId = StaffUserId, StartUtc = At(NextMonday, 10), EndUtc = At(NextMonday, 11) };

    private IEnumerable<string> Errors(CreateAppointmentCommand command) =>
        _validator.Validate(command).Errors.Select(e => e.ErrorMessage);

    [Test]
    public void Validate_Request_Passes()
    {
        _validator.Validate(Request()).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_Booking_Passes()
    {
        _validator.Validate(Booking()).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_NoProcedureOrStageOrNotes_Passes()
    {
        _validator.Validate(Request() with { ProcedureId = null, StageId = null, Notes = null }).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_NoPatient_Fails()
    {
        Errors(Request() with { PatientId = Guid.Empty }).Should().Equal("Patient is required.");
    }

    [Test]
    public void Validate_StageWithoutItsProcedure_Fails()
    {
        Errors(Request() with { ProcedureId = null }).Should().Equal("A stage needs its procedure.");
    }

    [Test]
    public void Validate_StaffWithoutATime_Fails()
    {
        Errors(Request() with { StaffUserId = StaffUserId }).Should().Equal(CreateAppointmentValidator.BookOrRequest);
    }

    [Test]
    public void Validate_TimeWithoutStaff_Fails()
    {
        Errors(Booking() with { StaffUserId = null }).Should().Equal(CreateAppointmentValidator.BookOrRequest);
    }

    [Test]
    public void Validate_StartWithoutAnEnd_Fails()
    {
        Errors(Booking() with { EndUtc = null }).Should().Equal(CreateAppointmentValidator.BookOrRequest);
    }

    [Test]
    public void Validate_EmptyStaffMember_Fails()
    {
        Errors(Booking() with { StaffUserId = Guid.Empty }).Should().Equal("Member of staff is required.");
    }

    [Test]
    public void Validate_BookingInThePast_Fails()
    {
        Errors(Booking() with { StartUtc = At(Today, 8), EndUtc = At(Today, 8, 30) }).Should().Equal(BookingTimes.InThePast);
    }

    [Test]
    public void Validate_BookingAtTheWeekend_Fails()
    {
        Errors(Booking() with { StartUtc = At(NextSunday, 10), EndUtc = At(NextSunday, 11) }).Should().Equal(WorkingHours.Weekend);
    }

    [Test]
    public void Validate_BookingOutsideTheWorkingDay_Fails()
    {
        Errors(Booking() with { StartUtc = At(NextMonday, 7), EndUtc = At(NextMonday, 9) }).Should().Equal(WorkingHours.OutsideWorkingDay);
    }

    [Test]
    public void Validate_NotesAtTheLimit_Passes()
    {
        _validator.Validate(Request() with { Notes = new string('a', AppointmentLimits.MaxNotesLength) }).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_NotesTooLong_Fails()
    {
        Errors(Request() with { Notes = new string('a', AppointmentLimits.MaxNotesLength + 1) })
            .Should().Equal("Notes must not exceed 2000 characters.");
    }
}
