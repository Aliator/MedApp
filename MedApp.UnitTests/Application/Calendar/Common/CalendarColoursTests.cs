using FluentAssertions;
using MedApp.Application.Calendar.Common;
using static MedApp.UnitTests.Common.Constants.CalendarSetupTestConstants;

namespace MedApp.UnitTests.Application.Calendar.Common;

[TestFixture]
public sealed class CalendarColoursTests
{
    [Test]
    public void Palette_IsTheAstryxTokenColours()
    {
        CalendarColours.Palette.Should().Equal("red", "orange", "yellow", "green", "teal", "cyan", "blue", "purple", "pink", "gray");
    }

    [Test]
    public void Names_ListsThePaletteForMessages()
    {
        CalendarColours.Names.Should().Be("red, orange, yellow, green, teal, cyan, blue, purple, pink, gray");
    }

    [TestCase("green")]
    [TestCase("GREEN")]
    [TestCase(UntidyColour)]
    public void IsColour_PaletteColourInAnyCaseOrSpacing_IsTrue(string value)
    {
        CalendarColours.IsColour(value).Should().BeTrue();
    }

    [TestCase(NotAColour)]
    [TestCase("grey")]
    [TestCase("")]
    [TestCase("  ")]
    [TestCase("#00ff00")]
    [TestCase(null)]
    public void IsColour_AnythingElse_IsFalse(string? value)
    {
        CalendarColours.IsColour(value).Should().BeFalse();
    }

    [TestCase(UntidyColour, TidyColour)]
    [TestCase("PURPLE", "purple")]
    [TestCase("red", "red")]
    public void Normalise_StoresLowerCaseTrimmed(string value, string stored)
    {
        CalendarColours.Normalise(value).Should().Be(stored);
    }
}
