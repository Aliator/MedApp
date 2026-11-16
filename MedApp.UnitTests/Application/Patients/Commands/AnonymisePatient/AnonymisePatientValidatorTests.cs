using FluentAssertions;
using MedApp.Application.Patients.Commands.AnonymisePatient;

namespace MedApp.UnitTests.Application.Patients.Commands.AnonymisePatient;

[TestFixture]
public sealed class AnonymisePatientValidatorTests
{
    private readonly AnonymisePatientValidator _validator = new();

    [TestCase("RetentionPeriodEnded")]
    [TestCase("erasureRequest")]
    [TestCase("CreatedInError")]
    public void Validate_AReasonFromTheListAndConfirmation_IsValid(string reason)
    {
        _validator.Validate(new AnonymisePatientCommand(Guid.NewGuid(), reason, null, "Test Patient")).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_Missing_IsInvalid()
    {
        var result = _validator.Validate(new AnonymisePatientCommand(Guid.NewGuid(), "", null, ""));

        result.Errors.Select(e => e.PropertyName).Should().BeEquivalentTo(["Reason", "Confirmation"]);
    }

    [TestCase("Past its retention period")]
    [TestCase("1")]
    [TestCase("Deleted")]
    public void Validate_AReasonNotInTheList_IsInvalid(string reason)
    {
        _validator.Validate(new AnonymisePatientCommand(Guid.NewGuid(), reason, null, "x"))
            .Errors.Should().ContainSingle(e => e.PropertyName == "Reason")
            .Which.ErrorMessage.Should().Contain(AnonymisePatientValidator.ReasonNames);
    }

    [Test]
    public void Validate_LongNote_IsInvalid()
    {
        _validator.Validate(new AnonymisePatientCommand(Guid.NewGuid(), "ErasureRequest", new string('a', AnonymisePatientValidator.MaxNoteLength + 1), "x"))
            .Errors.Should().ContainSingle(e => e.PropertyName == "Note");
    }

    [Test]
    public void Validate_NoteAtTheLimit_IsValid()
    {
        _validator.Validate(new AnonymisePatientCommand(Guid.NewGuid(), "ErasureRequest", new string('a', AnonymisePatientValidator.MaxNoteLength), "x"))
            .IsValid.Should().BeTrue();
    }
}
