using FluentAssertions;
using MedApp.Application.Patients.Commands.CreatePatient;
using MedApp.UnitTests.Common;

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
            TestConstants.Patients.ValidFirstName,
            TestConstants.Patients.ValidLastName,
            TestConstants.Patients.ValidDateOfBirth,
            TestConstants.Patients.ValidEmail
        );

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_EmptyFirstName_Fails()
    {
        var command = new CreatePatientCommand(
            "",
            TestConstants.Patients.ValidLastName,
            TestConstants.Patients.ValidDateOfBirth,
            TestConstants.Patients.ValidEmail
        );

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "FirstName");
    }

    [Test]
    public void Validate_FirstNameTooLong_Fails()
    {
        var command = new CreatePatientCommand(
            new string('a', TestConstants.Patients.MaxNameLength + 1),
            TestConstants.Patients.ValidLastName,
            TestConstants.Patients.ValidDateOfBirth,
            TestConstants.Patients.ValidEmail
        );

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "FirstName");
    }

    [Test]
    public void Validate_EmptyLastName_Fails()
    {
        var command = new CreatePatientCommand(
            TestConstants.Patients.ValidFirstName,
            "",
            TestConstants.Patients.ValidDateOfBirth,
            TestConstants.Patients.ValidEmail
        );

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "LastName");
    }

    [Test]
    public void Validate_LastNameTooLong_Fails()
    {
        var command = new CreatePatientCommand(
            TestConstants.Patients.ValidFirstName,
            new string('a', TestConstants.Patients.MaxNameLength + 1),
            TestConstants.Patients.ValidDateOfBirth,
            TestConstants.Patients.ValidEmail
        );

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "LastName");
    }

    [Test]
    public void Validate_DefaultDateOfBirth_Fails()
    {
        var command = new CreatePatientCommand(
            TestConstants.Patients.ValidFirstName,
            TestConstants.Patients.ValidLastName,
            default,
            TestConstants.Patients.ValidEmail
        );

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "DateOfBirth");
    }

    [Test]
    public void Validate_FutureDateOfBirth_Fails()
    {
        var command = new CreatePatientCommand(
            TestConstants.Patients.ValidFirstName,
            TestConstants.Patients.ValidLastName,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            TestConstants.Patients.ValidEmail
        );

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "DateOfBirth");
    }

    [Test]
    public void Validate_EmptyEmail_Fails()
    {
        var command = new CreatePatientCommand(
            TestConstants.Patients.ValidFirstName,
            TestConstants.Patients.ValidLastName,
            TestConstants.Patients.ValidDateOfBirth,
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
            TestConstants.Patients.ValidFirstName,
            TestConstants.Patients.ValidLastName,
            TestConstants.Patients.ValidDateOfBirth,
            "not-an-email"
        );

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email");
    }
}
