using FluentAssertions;
using MedApp.Application.Appointments.Commands.RecordAppointmentOutcome;
using static MedApp.UnitTests.Common.Constants.AppointmentsTestConstants;

namespace MedApp.UnitTests.Application.Appointments.Commands.RecordAppointmentOutcome;

[TestFixture]
public sealed class RecordAppointmentOutcomeValidatorTests
{
    private const string OutcomeMessage = "Outcome must be one of: Attended, DidNotAttend.";

    private readonly RecordAppointmentOutcomeValidator _validator = new();

    private IEnumerable<string> Errors(RecordAppointmentOutcomeCommand command) =>
        _validator.Validate(command).Errors.Select(e => e.ErrorMessage);

    [TestCase("Attended")]
    [TestCase("didnotattend")]
    [TestCase(" DidNotAttend ")]
    public void Validate_AnOutcome_Passes(string outcome)
    {
        _validator.Validate(new RecordAppointmentOutcomeCommand(Guid.NewGuid(), outcome, Colleague)).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_NoAppointment_Fails()
    {
        Errors(new RecordAppointmentOutcomeCommand(Guid.Empty, "Attended", Colleague)).Should().Equal("Appointment id is required.");
    }

    [TestCase("Booked")]
    [TestCase("Cancelled")]
    [TestCase("Requested")]
    [TestCase("2")]
    [TestCase("")]
    [TestCase("Maybe")]
    public void Validate_NotAnOutcome_Fails(string outcome)
    {
        Errors(new RecordAppointmentOutcomeCommand(Guid.NewGuid(), outcome, Colleague)).Should().Equal(OutcomeMessage);
    }
}
