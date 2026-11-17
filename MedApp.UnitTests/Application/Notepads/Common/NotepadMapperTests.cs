using FluentAssertions;
using MedApp.Application.Notepads.Common;
using static MedApp.UnitTests.Common.Constants.NotepadTestConstants;

namespace MedApp.UnitTests.Application.Notepads.Common;

[TestFixture]
public sealed class NotepadMapperTests
{
    [Test]
    public void ToResponse_Notepad_HasItsTextTimeAndVersion()
    {
        var notepad = CreateNotepad(daysAgo: 2, version: 7);

        NotepadMapper.ToResponse(notepad, KeepDays).Should().BeEquivalentTo(new
        {
            Text, LastUpdatedUtc = (DateTime?)Now.AddDays(-2), Version = 7, KeepDays
        });
    }

    [Test]
    public void ToResponse_None_IsEmptyAtVersionZero()
    {
        NotepadMapper.ToResponse(null, 14).Should().BeEquivalentTo(new
        {
            Text = string.Empty, LastUpdatedUtc = (DateTime?)null, Version = 0, KeepDays = 14
        });
    }

    [TestCase(KeepDays - 0.001, false)]
    [TestCase(KeepDays, true)]
    [TestCase(KeepDays + 1, true)]
    public void IsExpired_AfterKeepDaysWithoutAChange(double daysAgo, bool expected)
    {
        NotepadRules.IsExpired(CreateNotepad(daysAgo), Now, KeepDays).Should().Be(expected);
    }

    [Test]
    public void ClearedMessage_SaysHowManyDays()
    {
        NotepadRules.ClearedMessage(30).Should().Be("Your notepad was cleared after 30 days without changes. Reload it to start again.");
    }
}
