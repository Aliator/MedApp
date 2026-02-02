using FluentAssertions;
using MedApp.Application.Patients.Commands.UpdatePatient;
using MedApp.UnitTests.Common;

namespace MedApp.UnitTests.Application.Patients.Validators;

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
            TestConstants.Patients.ValidFirstName,
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
            new string('a', TestConstants.Patients.MaxNameLength + 1),
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
            new string('a', TestConstants.Patients.MaxNameLength + 1),
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
            "not-an-email"
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
            TestConstants.Patients.ValidFirstName,
            TestConstants.Patients.ValidLastName,
            TestConstants.Patients.ValidDateOfBirth,
            TestConstants.Patients.ValidEmail
        );

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }
}
