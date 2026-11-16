using FluentAssertions;
using MedApp.Application.Patients.Commands.SetPatientFlag;

namespace MedApp.UnitTests.Application.Patients.Commands.SetPatientFlag;

[TestFixture]
public sealed class SetPatientFlagValidatorTests
{
    private readonly SetPatientFlagValidator _validator = new();

    [Test]
    public void Validate_SetWithAReason_IsValid()
    {
        _validator.Validate(new SetPatientFlagCommand(Guid.NewGuid(), PatientFlag.Restricted, true, "Why")).IsValid.Should().BeTrue();
    }

    [TestCase(null)]
    [TestCase("")]
    public void Validate_SetWithoutAReason_IsInvalid(string? reason)
    {
        _validator.Validate(new SetPatientFlagCommand(Guid.NewGuid(), PatientFlag.Disputed, true, reason))
            .Errors.Should().ContainSingle(e => e.PropertyName == "Reason");
    }

    [Test]
    public void Validate_ClearWithoutAReason_IsValid()
    {
        _validator.Validate(new SetPatientFlagCommand(Guid.NewGuid(), PatientFlag.Restricted, false, null)).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_LongReason_IsInvalid()
    {
        _validator.Validate(new SetPatientFlagCommand(Guid.NewGuid(), PatientFlag.Restricted, false,
                new string('a', SetPatientFlagValidator.MaxReasonLength + 1)))
            .Errors.Should().ContainSingle(e => e.PropertyName == "Reason");
    }

    [Test]
    public void Validate_UnknownFlag_IsInvalid()
    {
        _validator.Validate(new SetPatientFlagCommand(Guid.NewGuid(), (PatientFlag)9, true, "Why"))
            .Errors.Should().ContainSingle(e => e.PropertyName == "Flag");
    }
}
