using FluentAssertions;
using MedApp.Application.Patients.Commands.CreatePatient;
using MedApp.UnitTests.Common.Constants;

namespace MedApp.UnitTests.Application.Patients.Commands.CreatePatient;

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
    public void Validate_ValidCommand_Passes()
    {
        var command = new CreatePatientCommand(
            PatientsTestConstants.ValidFirstName,
            PatientsTestConstants.ValidLastName,
            PatientsTestConstants.ValidDateOfBirth,
            PatientsTestConstants.ValidEmail
        );

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_EmptyFirstName_Fails()
    {
        var command = new CreatePatientCommand(
            "",
            PatientsTestConstants.ValidLastName,
            PatientsTestConstants.ValidDateOfBirth,
            PatientsTestConstants.ValidEmail
        );

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "FirstName");
    }

    [Test]
    public void Validate_FirstNameTooLong_Fails()
    {
        var command = new CreatePatientCommand(
            new string('a', PatientsTestConstants.MaxNameLength + 1),
            PatientsTestConstants.ValidLastName,
            PatientsTestConstants.ValidDateOfBirth,
            PatientsTestConstants.ValidEmail
        );

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "FirstName");
    }

    [Test]
    public void Validate_EmptyLastName_Fails()
    {
        var command = new CreatePatientCommand(
            PatientsTestConstants.ValidFirstName,
            "",
            PatientsTestConstants.ValidDateOfBirth,
            PatientsTestConstants.ValidEmail
        );

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "LastName");
    }

    [Test]
    public void Validate_LastNameTooLong_Fails()
    {
        var command = new CreatePatientCommand(
            PatientsTestConstants.ValidFirstName,
            new string('a', PatientsTestConstants.MaxNameLength + 1),
            PatientsTestConstants.ValidDateOfBirth,
            PatientsTestConstants.ValidEmail
        );

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "LastName");
    }

    [Test]
    public void Validate_DefaultDateOfBirth_Fails()
    {
        var command = new CreatePatientCommand(
            PatientsTestConstants.ValidFirstName,
            PatientsTestConstants.ValidLastName,
            default,
            PatientsTestConstants.ValidEmail
        );

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "DateOfBirth");
    }

    [Test]
    public void Validate_FutureDateOfBirth_Fails()
    {
        var command = new CreatePatientCommand(
            PatientsTestConstants.ValidFirstName,
            PatientsTestConstants.ValidLastName,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            PatientsTestConstants.ValidEmail
        );

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "DateOfBirth");
    }

    [Test]
    public void Validate_EmptyEmail_Fails()
    {
        var command = new CreatePatientCommand(
            PatientsTestConstants.ValidFirstName,
            PatientsTestConstants.ValidLastName,
            PatientsTestConstants.ValidDateOfBirth,
            ""
        );

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email");
    }

    [Test]
    public void Validate_InvalidEmail_Fails()
    {
        var command = new CreatePatientCommand(
            PatientsTestConstants.ValidFirstName,
            PatientsTestConstants.ValidLastName,
            PatientsTestConstants.ValidDateOfBirth,
            PatientsTestConstants.InvalidEmail
        );

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email");
    }
}
