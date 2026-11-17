using FluentAssertions;
using MedApp.Application.Notepads.Common;

namespace MedApp.UnitTests.Application.Notepads.Common;

[TestFixture]
public sealed class NotepadOptionsTests
{
    [TestCase(-1, false)]
    [TestCase(0, false)]
    [TestCase(1, true)]
    [TestCase(30, true)]
    public void IsValid_KeepsANotepadAtLeastADay(int keepDays, bool expected)
    {
        new NotepadOptions { KeepDays = keepDays }.IsValid.Should().Be(expected);
    }

    [Test]
    public void Defaults_AreValid()
    {
        new NotepadOptions().IsValid.Should().BeTrue();
    }
}
