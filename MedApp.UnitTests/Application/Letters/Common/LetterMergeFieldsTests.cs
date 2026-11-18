using FluentAssertions;
using MedApp.Application.Letters.Common;

namespace MedApp.UnitTests.Application.Letters.Common;

[TestFixture]
public sealed class LetterMergeFieldsTests
{
    private static readonly Dictionary<string, string?> Values = new()
    {
        [LetterMergeFields.PatientName] = "Sam *Example*",
        [LetterMergeFields.PatientAddress] = "1 Imaginary Lane\r\n\nFictionton\nZZ1 1ZZ",
        [LetterMergeFields.AuthorName] = "Alex Author",
        [LetterMergeFields.ApproverName] = "  ",
    };

    [Test]
    public void All_HasTheFixedListOfIdentityAndAdministrativeFields_EachOnce()
    {
        LetterMergeFields.All.Select(f => f.Name).Should().Equal(
            "patient.name", "patient.dateOfBirth", "patient.nhsNumber", "patient.address", "practice.name",
            "practice.address", "referral.receivedDate", "appointment.lastAttendedDate", "author.name", "author.jobTitle",
            "approver.name", "approver.jobTitle", "today", "site.name");
        LetterMergeFields.All.Should().OnlyContain(f => f.Label.Length > 0 && f.Description.Length > 0 && f.Example.Length > 0);
    }

    [Test]
    public void Examples_AreSynthetic_WithANhsNumberFromThe999Range() =>
        LetterMergeFields.All.Single(f => f.Name == LetterMergeFields.PatientNhsNumber).Example.Should().StartWith("999");

    [Test]
    public void Find_ReturnsEveryNameOnceInOrder_KnownOrNot_WithSpacesInsideTheBraces() =>
        LetterMergeFields.Find("{{ today }} {{patient.name}} {{patient.nme}} {{today}} {{}} {single}")
            .Should().Equal("today", "patient.name", "patient.nme");

    [TestCase(null)]
    [TestCase("")]
    public void Find_NoText_FindsNothing(string? text) => LetterMergeFields.Find(text).Should().BeEmpty();

    [Test]
    public void Used_ReturnsKnownNamesAcrossTexts_EachOnce() =>
        LetterMergeFields.Used("{{site.name}}", null, "{{patient.nme}} {{author.name}} {{site.name}}")
            .Should().Equal("site.name", "author.name");

    [Test]
    public void UnknownMessage_NamesTheUnknownFields()
    {
        LetterMergeFields.UnknownMessage("{{patient.name}}").Should().BeNull();
        LetterMergeFields.UnknownMessage("{{patient.nme}}").Should().StartWith("{{patient.nme}} isn't a merge field.");
        LetterMergeFields.UnknownMessage("{{a}} {{b}} {{a}}").Should().StartWith("{{a}}, {{b}} aren't merge fields.");
    }

    [Test]
    public void IsKnown_IsCaseSensitive()
    {
        LetterMergeFields.IsKnown("patient.name").Should().BeTrue();
        LetterMergeFields.IsKnown("Patient.Name").Should().BeFalse();
    }

    [Test]
    public void SampleValues_FillEveryField_WithTodaysRealDate()
    {
        var values = LetterMergeFields.SampleValues(new DateOnly(2027, 2, 3));

        values.Keys.Should().BeEquivalentTo(LetterMergeFields.All.Select(f => f.Name));
        values[LetterMergeFields.Today].Should().Be("3 February 2027");
        values[LetterMergeFields.PatientName].Should().Be("Sam Example");
    }

    [Test]
    public void FillPlain_FillsValuesAsTheyAre_KeepingTheirLines_AndListsMissingOnes()
    {
        var result = LetterMergeFields.FillPlain(
            "To {{patient.name}}\n{{patient.address}}\nFrom {{approver.name}}{{site.name}} {{site.name}} {{other}}", Values);

        result.Text.Should().Be("To Sam *Example*\n1 Imaginary Lane\nFictionton\nZZ1 1ZZ\nFrom   {{other}}");
        result.Missing.Should().Equal("approver.name", "site.name");
    }

    [Test]
    public void FillMarkdown_EscapesValues_AndKeepsTheirLinesAsHardBreaks()
    {
        var result = LetterMergeFields.FillMarkdown("Re: {{patient.name}}, {{patient.address}}", Values);

        result.Text.Should().Be("Re: Sam \\*Example\\*, 1 Imaginary Lane\\\nFictionton\\\nZZ1 1ZZ");
        result.Missing.Should().BeEmpty();
    }

    [TestCase(null)]
    [TestCase("")]
    public void Fill_NoText_IsEmpty(string? text)
    {
        LetterMergeFields.FillPlain(text, Values).Should().BeEquivalentTo(new LetterMergeResult(string.Empty, []));
        LetterMergeFields.FillMarkdown(text, Values).Text.Should().BeEmpty();
    }

    [Test]
    public void EscapeMarkdown_EscapesAsciiPunctuationAndSymbols_Only() =>
        LetterMergeFields.EscapeMarkdown("O'Brien [x] #1 <b> Zoë – 3+4").Should().Be("O\\'Brien \\[x\\] \\#1 \\<b\\> Zoë – 3\\+4");

    [Test]
    public void SplitLines_TrimsAndDropsBlankLines()
    {
        LetterMergeFields.SplitLines(" a \r\n\n b ").Should().Equal("a", "b");
        LetterMergeFields.SplitLines(null).Should().BeEmpty();
    }

    [Test]
    public void FormatDate_IsTheUkLongDate() =>
        LetterMergeFields.FormatDate(new DateOnly(1970, 1, 1)).Should().Be("1 January 1970");
}
