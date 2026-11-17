using FluentAssertions;
using MedApp.Application.Notepads.Commands.SaveNotepad;
using MedApp.Domain.Notepads;
using static MedApp.UnitTests.Common.Constants.NotepadTestConstants;

namespace MedApp.UnitTests.Application.Notepads.Commands.SaveNotepad;

[TestFixture]
public sealed class SaveNotepadValidatorTests
{
    private readonly SaveNotepadValidator _validator = new();

    [TestCase("")]
    [TestCase(Text)]
    public void Validate_Text_Passes(string text)
    {
        _validator.Validate(new SaveNotepadCommand(UserId, text, 0)).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_Longest_Passes()
    {
        _validator.Validate(new SaveNotepadCommand(UserId, new string('a', Notepad.MaxTextLength), 2)).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_TooLong_Fails()
    {
        _validator.Validate(new SaveNotepadCommand(UserId, new string('a', Notepad.MaxTextLength + 1), 2))
            .Errors.Should().ContainSingle(e => e.PropertyName == "Text");
    }

    [Test]
    public void Validate_NullText_Fails()
    {
        _validator.Validate(new SaveNotepadCommand(UserId, null!, 0)).Errors.Should().ContainSingle(e => e.PropertyName == "Text");
    }

    [Test]
    public void Validate_NegativeVersion_Fails()
    {
        _validator.Validate(new SaveNotepadCommand(UserId, Text, -1)).Errors.Should().ContainSingle(e => e.PropertyName == "ExpectedVersion");
    }

    [Test]
    public void Validate_NoUser_Fails()
    {
        _validator.Validate(new SaveNotepadCommand(Guid.Empty, Text, 0)).Errors.Should().ContainSingle(e => e.PropertyName == "UserId");
    }
}
