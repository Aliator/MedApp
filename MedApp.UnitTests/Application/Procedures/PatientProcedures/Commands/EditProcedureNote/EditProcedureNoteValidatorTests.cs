using FluentAssertions;
using MedApp.Application.Procedures.PatientProcedures.Commands.EditProcedureNote;
using MedApp.Domain.Procedures.PatientProcedures;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;

namespace MedApp.UnitTests.Application.Procedures.PatientProcedures.Commands.EditProcedureNote;

[TestFixture]
public sealed class EditProcedureNoteValidatorTests
{
    private readonly EditProcedureNoteValidator _validator = new();

    private static EditProcedureNoteCommand Valid() => new(Guid.NewGuid(), Guid.NewGuid(), NoteText, 1, Staff);

    [Test]
    public void Validate_Edit_Passes()
    {
        _validator.Validate(Valid()).IsValid.Should().BeTrue();
    }

    [TestCase("")]
    [TestCase("   ")]
    public void Validate_BlankNote_Fails(string text)
    {
        _validator.Validate(Valid() with { Text = text }).Errors.Should().ContainSingle(e => e.PropertyName == "Text");
    }

    [Test]
    public void Validate_NoteTooLong_Fails()
    {
        _validator.Validate(Valid() with { Text = new string('a', PatientProcedureNote.MaxTextLength + 1) })
            .Errors.Should().ContainSingle(e => e.PropertyName == "Text");
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void Validate_NoVersion_Fails(int version)
    {
        _validator.Validate(Valid() with { ExpectedVersion = version })
            .Errors.Should().ContainSingle(e => e.PropertyName == "ExpectedVersion");
    }

    [Test]
    public void Validate_NoNote_Fails()
    {
        _validator.Validate(Valid() with { NoteId = Guid.Empty }).Errors.Should().ContainSingle(e => e.PropertyName == "NoteId");
    }
}
