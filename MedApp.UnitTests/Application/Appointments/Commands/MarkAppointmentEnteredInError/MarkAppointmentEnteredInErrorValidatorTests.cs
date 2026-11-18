using FluentAssertions;
using MedApp.Application.Appointments.Commands.MarkAppointmentEnteredInError;
using MedApp.Application.Appointments.Common;
using static MedApp.UnitTests.Common.Constants.AppointmentsTestConstants;

namespace MedApp.UnitTests.Application.Appointments.Commands.MarkAppointmentEnteredInError;

[TestFixture]
public sealed class MarkAppointmentEnteredInErrorValidatorTests
{
    private readonly MarkAppointmentEnteredInErrorValidator _validator = new();

    private IEnumerable<string> Errors(MarkAppointmentEnteredInErrorCommand command) =>
        _validator.Validate(command).Errors.Select(e => e.ErrorMessage);

    [Test]
    public void Validate_AReason_Passes()
    {
        _validator.Validate(new MarkAppointmentEnteredInErrorCommand(Guid.NewGuid(), "Wrong patient.", Colleague, false)).IsValid.Should().BeTrue();
    }

    [TestCase("")]
    [TestCase("  ")]
    public void Validate_NoReason_Fails(string reason)
    {
        Errors(new MarkAppointmentEnteredInErrorCommand(Guid.NewGuid(), reason, Colleague, false))
            .Should().Equal("A reason is required to mark an appointment entered in error.");
    }

    [Test]
    public void Validate_ReasonTooLong_Fails()
    {
        Errors(new MarkAppointmentEnteredInErrorCommand(Guid.NewGuid(), new string('a', AppointmentLimits.MaxCancelReasonLength + 1), Colleague, false))
            .Should().Equal($"Reason must not exceed {AppointmentLimits.MaxCancelReasonLength} characters.");
    }

    [Test]
    public void Validate_NoAppointment_Fails()
    {
        Errors(new MarkAppointmentEnteredInErrorCommand(Guid.Empty, "Wrong patient.", Colleague, false)).Should().Equal("Appointment id is required.");
    }
}
