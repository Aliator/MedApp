using FluentAssertions;
using MedApp.Application.Patients.Commands.UpdatePatient;
using MedApp.UnitTests.Common.Constants;

namespace MedApp.UnitTests.Application.Patients.Commands.UpdatePatient;

[TestFixture]
public sealed class UpdatePatientValidatorTests
{
    private UpdatePatientValidator _validator = null!;

    [SetUp]
    public void SetUp()
    {
        _validator = new UpdatePatientValidator();
    }

    [Test]
    public void Validate_AtLeastOneFieldProvided_Passes()
    {
        var command = new UpdatePatientCommand(
            Guid.NewGuid(),
            PatientsTestConstants.ValidFirstName,
            null,
            null,
            null
        );

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_NoFieldsProvided_Fails()
    {
        var command = new UpdatePatientCommand(
            Guid.NewGuid(),
            null,
            null,
            null,
            null
        );

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(
            e => e.ErrorMessage == "At least one field must be provided.");
    }

    [Test]
    public void Validate_EmptyFirstName_Fails()
    {
        var command = new UpdatePatientCommand(
            Guid.NewGuid(),
            "",
            null,
            null,
            null
        );

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "FirstName");
    }

    [Test]
    public void Validate_FirstNameTooLong_Fails()
    {
        var command = new UpdatePatientCommand(
            Guid.NewGuid(),
            new string('a', PatientsTestConstants.MaxNameLength + 1),
            null,
            null,
            null
        );

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "FirstName");
    }

    [Test]
    public void Validate_EmptyLastName_Fails()
    {
        var command = new UpdatePatientCommand(
            Guid.NewGuid(),
            null,
            "",
            null,
            null
        );

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "LastName");
    }

    [Test]
    public void Validate_LastNameTooLong_Fails()
    {
        var command = new UpdatePatientCommand(
            Guid.NewGuid(),
            null,
            new string('a', PatientsTestConstants.MaxNameLength + 1),
            null,
            null
        );

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "LastName");
    }

    [Test]
    public void Validate_FutureDateOfBirth_Fails()
    {
        var command = new UpdatePatientCommand(
            Guid.NewGuid(),
            null,
            null,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            null
        );

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "DateOfBirth");
    }

    [Test]
    public void Validate_InvalidEmail_Fails()
    {
        var command = new UpdatePatientCommand(
            Guid.NewGuid(),
            null,
            null,
            null,
            PatientsTestConstants.InvalidEmail
        );

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email");
    }

    [Test]
    public void Validate_WhitespaceEmail_Fails()
    {
        var command = new UpdatePatientCommand(
            Guid.NewGuid(),
            null,
            null,
            null,
            "   "
        );

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email");
    }

    [Test]
    public void Validate_MultipleFieldsProvided_Passes()
    {
        var command = new UpdatePatientCommand(
            Guid.NewGuid(),
            PatientsTestConstants.ValidFirstName,
            PatientsTestConstants.ValidLastName,
            PatientsTestConstants.ValidDateOfBirth,
            PatientsTestConstants.ValidEmail
        );

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }
}
