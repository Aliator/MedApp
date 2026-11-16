using FluentAssertions;
using MedApp.Application.Patients.Commands.AnonymisePatient;

namespace MedApp.UnitTests.Application.Patients.Commands.AnonymisePatient;

[TestFixture]
public sealed class AnonymisePatientValidatorTests
{
    private readonly AnonymisePatientValidator _validator = new();

    [Test]
    public void Validate_ReasonAndConfirmation_IsValid()
    {
        _validator.Validate(new AnonymisePatientCommand(Guid.NewGuid(), "Why", "Test Patient")).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_Missing_IsInvalid()
    {
        var result = _validator.Validate(new AnonymisePatientCommand(Guid.NewGuid(), "", ""));

        result.Errors.Select(e => e.PropertyName).Should().BeEquivalentTo(["Reason", "Confirmation"]);
    }

    [Test]
    public void Validate_LongReason_IsInvalid()
    {
        _validator.Validate(new AnonymisePatientCommand(Guid.NewGuid(), new string('a', AnonymisePatientValidator.MaxReasonLength + 1), "x"))
            .Errors.Should().ContainSingle(e => e.PropertyName == "Reason");
    }
}
