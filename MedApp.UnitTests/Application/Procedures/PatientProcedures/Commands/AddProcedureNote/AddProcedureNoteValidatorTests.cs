using FluentAssertions;
using MedApp.Application.Procedures.PatientProcedures.Commands.AddProcedureNote;
using MedApp.Domain.Procedures.PatientProcedures;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;

namespace MedApp.UnitTests.Application.Procedures.PatientProcedures.Commands.AddProcedureNote;

[TestFixture]
public sealed class AddProcedureNoteValidatorTests
{
    private readonly AddProcedureNoteValidator _validator = new();

    [Test]
    public void Validate_Note_Passes()
    {
        _validator.Validate(new AddProcedureNoteCommand(Guid.NewGuid(), NoteText, Staff)).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_LongestNote_Passes()
    {
        var command = new AddProcedureNoteCommand(Guid.NewGuid(), new string('a', PatientProcedureNote.MaxTextLength), Staff);

        _validator.Validate(command).IsValid.Should().BeTrue();
    }

    [TestCase("")]
    [TestCase("  \n\t ")]
    public void Validate_BlankNote_Fails(string text)
    {
        var result = _validator.Validate(new AddProcedureNoteCommand(Guid.NewGuid(), text, Staff));

        result.Errors.Should().ContainSingle(e => e.PropertyName == "Text").Which.ErrorMessage.Should().Be("Write something in the note.");
    }

    [Test]
    public void Validate_NoteTooLong_Fails()
    {
        var command = new AddProcedureNoteCommand(Guid.NewGuid(), new string('a', PatientProcedureNote.MaxTextLength + 1), Staff);

        _validator.Validate(command).Errors.Should().ContainSingle(e => e.PropertyName == "Text");
    }

    [Test]
    public void Validate_NoProcedure_Fails()
    {
        _validator.Validate(new AddProcedureNoteCommand(Guid.Empty, NoteText, Staff)).Errors
            .Should().ContainSingle(e => e.PropertyName == "ProcedureId");
    }
}
