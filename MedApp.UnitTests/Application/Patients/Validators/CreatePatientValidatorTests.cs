using FluentAssertions;
using MedApp.Application.Patients.Commands.CreatePatient;

namespace MedApp.UnitTests.Application.Patients.Validators;

[TestFixture]
public sealed class CreatePatientValidatorTests
{
    private CreatePatientValidator _validator = null!;

    [SetUp]
    public void SetUp()
    {
        _validator = new CreatePatientValidator();
    }

    [Test]
    public void Validate_EmptyEmail_Fails()
    {
        var command = new CreatePatientCommand(
            "John",
            "Smith",
            new DateOnly(1990, 1, 1),
            ""
        );

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
    }
}