using FluentAssertions;
using MedApp.Application.Appointments.Commands.CancelAppointment;
using MedApp.Application.Appointments.Common;
using static MedApp.UnitTests.Common.Constants.AppointmentsTestConstants;

namespace MedApp.UnitTests.Application.Appointments.Commands.CancelAppointment;

[TestFixture]
public sealed class CancelAppointmentValidatorTests
{
    private readonly CancelAppointmentValidator _validator = new();

    private static CancelAppointmentCommand Valid() => new(Guid.NewGuid(), CancelReason, Colleague);

    private IEnumerable<string> Errors(CancelAppointmentCommand command) =>
        _validator.Validate(command).Errors.Select(e => e.ErrorMessage);

    [Test]
    public void Validate_WithAReason_Passes()
    {
        _validator.Validate(Valid()).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_NoAppointment_Fails()
    {
        Errors(Valid() with { AppointmentId = Guid.Empty }).Should().Equal("Appointment id is required.");
    }

    [TestCase("")]
    [TestCase("   ")]
    public void Validate_NoReason_Fails(string reason)
    {
        Errors(Valid() with { Reason = reason }).Should().Equal("A reason is required to cancel an appointment.");
    }

    [Test]
    public void Validate_NullReason_Fails()
    {
        Errors(Valid() with { Reason = null! }).Should().Contain("A reason is required to cancel an appointment.");
    }

    [Test]
    public void Validate_ReasonAtTheLimit_Passes()
    {
        _validator.Validate(Valid() with { Reason = new string('a', AppointmentLimits.MaxCancelReasonLength) }).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_ReasonTooLong_Fails()
    {
        Errors(Valid() with { Reason = new string('a', AppointmentLimits.MaxCancelReasonLength + 1) })
            .Should().Equal("Reason must not exceed 500 characters.");
    }
}
