using FluentAssertions;
using MedApp.Application.Pathways.PatientPathways.Commands.AddPathwayNote;
using MedApp.Domain.Pathways.PatientPathways;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;

namespace MedApp.UnitTests.Application.Pathways.PatientPathways.Commands.AddPathwayNote;

[TestFixture]
public sealed class AddPathwayNoteValidatorTests
{
    private readonly AddPathwayNoteValidator _validator = new();

    [Test]
    public void Validate_Note_Passes()
    {
        _validator.Validate(new AddPathwayNoteCommand(Guid.NewGuid(), NoteText, Staff)).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_LongestNote_Passes()
    {
        var command = new AddPathwayNoteCommand(Guid.NewGuid(), new string('a', PatientPathwayNote.MaxTextLength), Staff);

        _validator.Validate(command).IsValid.Should().BeTrue();
    }

    [TestCase("")]
    [TestCase("  \n\t ")]
    public void Validate_BlankNote_Fails(string text)
    {
        var result = _validator.Validate(new AddPathwayNoteCommand(Guid.NewGuid(), text, Staff));

        result.Errors.Should().ContainSingle(e => e.PropertyName == "Text").Which.ErrorMessage.Should().Be("Write something in the note.");
    }

    [Test]
    public void Validate_NoteTooLong_Fails()
    {
        var command = new AddPathwayNoteCommand(Guid.NewGuid(), new string('a', PatientPathwayNote.MaxTextLength + 1), Staff);

        _validator.Validate(command).Errors.Should().ContainSingle(e => e.PropertyName == "Text");
    }

    [Test]
    public void Validate_NoPathway_Fails()
    {
        _validator.Validate(new AddPathwayNoteCommand(Guid.Empty, NoteText, Staff)).Errors
            .Should().ContainSingle(e => e.PropertyName == "PathwayId");
    }
}
