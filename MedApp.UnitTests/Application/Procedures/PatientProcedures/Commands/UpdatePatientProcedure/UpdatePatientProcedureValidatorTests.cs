using FluentAssertions;
using MedApp.Application.Procedures.PatientProcedures.Commands.UpdatePatientProcedure;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;

namespace MedApp.UnitTests.Application.Procedures.PatientProcedures.Commands.UpdatePatientProcedure;

[TestFixture]
public sealed class UpdatePatientProcedureValidatorTests
{
    private readonly UpdatePatientProcedureValidator _validator = new();

    [Test]
    public void Validate_NothingToChange_Passes()
    {
        _validator.Validate(new UpdatePatientProcedureCommand(Guid.NewGuid(), null, null, null, Staff)).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_BlankTitle_Fails()
    {
        _validator.Validate(new UpdatePatientProcedureCommand(Guid.NewGuid(), "  ", null, null, Staff)).IsValid.Should().BeFalse();
    }

    [Test]
    public void Validate_UnknownPriority_Fails()
    {
        _validator.Validate(new UpdatePatientProcedureCommand(Guid.NewGuid(), null, null, "Soon", Staff)).IsValid.Should().BeFalse();
    }

    [TestCase("")]
    [TestCase("Synthetic notes from a page loaded before the change.")]
    public void Validate_OldNotesField_FailsSayingWhereNotesGoNow(string notes)
    {
        var result = _validator.Validate(new UpdatePatientProcedureCommand(Guid.NewGuid(), null, null, null, Staff, notes));

        result.Errors.Should().ContainSingle().Which.ErrorMessage.Should().Be(UpdatePatientProcedureValidator.NotesMovedMessage);
    }
}
