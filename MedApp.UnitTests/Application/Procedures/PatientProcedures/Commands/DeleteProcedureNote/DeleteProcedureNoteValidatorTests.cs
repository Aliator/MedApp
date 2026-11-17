using FluentAssertions;
using MedApp.Application.Procedures.PatientProcedures.Commands.DeleteProcedureNote;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;

namespace MedApp.UnitTests.Application.Procedures.PatientProcedures.Commands.DeleteProcedureNote;

[TestFixture]
public sealed class DeleteProcedureNoteValidatorTests
{
    private readonly DeleteProcedureNoteValidator _validator = new();

    private static DeleteProcedureNoteCommand Command(string reason = DeletionReason, Guid? procedureId = null, Guid? noteId = null) =>
        new(procedureId ?? Guid.NewGuid(), noteId ?? Guid.NewGuid(), reason, Admin);

    [Test]
    public void Validate_ValidCommand_IsValid() =>
        _validator.Validate(Command()).IsValid.Should().BeTrue();

    [TestCase("")]
    [TestCase("   ")]
    public void Validate_NoReason_IsInvalid(string reason)
    {
        var result = _validator.Validate(Command(reason));

        result.Errors.Should().ContainSingle(e => e.PropertyName == "Reason" && e.ErrorMessage == "Say why the note should be deleted.");
    }

    [Test]
    public void Validate_ReasonAtTheLimit_IsValid() =>
        _validator.Validate(Command(new string('a', 500))).IsValid.Should().BeTrue();

    [Test]
    public void Validate_ReasonTooLong_IsInvalid() =>
        _validator.Validate(Command(new string('a', 501))).Errors.Should().ContainSingle(e => e.PropertyName == "Reason");

    [Test]
    public void Validate_NoIds_IsInvalid()
    {
        var result = _validator.Validate(Command(procedureId: Guid.Empty, noteId: Guid.Empty));

        result.Errors.Select(e => e.PropertyName).Should().BeEquivalentTo("ProcedureId", "NoteId");
    }
}
