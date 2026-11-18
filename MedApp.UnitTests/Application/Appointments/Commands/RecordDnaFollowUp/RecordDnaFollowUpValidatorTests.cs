using FluentAssertions;
using FluentValidation.TestHelper;
using MedApp.Application.Appointments.Commands.RecordDnaFollowUp;
using static MedApp.UnitTests.Common.Constants.AppointmentsTestConstants;

namespace MedApp.UnitTests.Application.Appointments.Commands.RecordDnaFollowUp;

[TestFixture]
public sealed class RecordDnaFollowUpValidatorTests
{
    private readonly RecordDnaFollowUpValidator _validator = new();

    private static RecordDnaFollowUpCommand Command(string decision = "Rebook", string? detail = null) =>
        new(Guid.NewGuid(), decision, detail, Colleague);

    [TestCase("Rebook")]
    [TestCase("ClinicianToReview")]
    [TestCase("clinicianToReview")]
    public void Validate_DecisionWithoutDetail_IsValid(string decision) =>
        _validator.TestValidate(Command(decision)).ShouldNotHaveAnyValidationErrors();

    [TestCase("")]
    [TestCase("Discharge")]
    [TestCase("0")]
    public void Validate_UnknownDecision_IsInvalid(string decision) =>
        _validator.TestValidate(Command(decision)).ShouldHaveValidationErrorFor(x => x.Decision)
            .WithErrorMessage($"Decision must be one of: {RecordDnaFollowUpValidator.DecisionNames}.");

    [Test]
    public void Validate_NoFurtherAppointmentWithoutDetail_IsInvalid() =>
        _validator.TestValidate(Command("NoFurtherAppointment", " ")).ShouldHaveValidationErrorFor(x => x.Detail)
            .WithErrorMessage(RecordDnaFollowUpValidator.DetailRequiredMessage);

    [Test]
    public void Validate_NoFurtherAppointmentWithDetail_IsValid() =>
        _validator.TestValidate(Command("NoFurtherAppointment", "Synthetic: agreed with the consultant.")).ShouldNotHaveAnyValidationErrors();

    [Test]
    public void Validate_DetailTooLong_IsInvalid() =>
        _validator.TestValidate(Command(detail: new string('x', 501))).ShouldHaveValidationErrorFor(x => x.Detail);

    [Test]
    public void Validate_NoAppointment_IsInvalid() =>
        _validator.TestValidate(Command() with { AppointmentId = Guid.Empty }).IsValid.Should().BeFalse();
}
