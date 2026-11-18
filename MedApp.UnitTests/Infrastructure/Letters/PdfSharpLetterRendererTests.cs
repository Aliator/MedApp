using System.Text;
using FluentAssertions;
using MedApp.Application.Letters.Common;
using MedApp.Infrastructure.Letters;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Fields;
using PdfSharp.Pdf.IO;

namespace MedApp.UnitTests.Infrastructure.Letters;

[TestFixture]
public sealed class PdfSharpLetterRendererTests
{
    private const string Identifiers = "Sam Example · Born 1 January 1970 · NHS number 999 000 0018";

    private static LetterDocument Letter(string body = "Dear Doctor,", bool largePrint = false, string? marking = "Draft, not approved") =>
        new("Clinic letter", ["Synthetic Specialist Centre", "1 Imaginary Lane"], ["Testbrook Surgery", "Fictionton"],
            body, ["Yours sincerely,", "", "Alex Author"], Identifiers, largePrint, marking);

    /// <summary>A paragraph's text, with line breaks as \n, tabs as \t, bold as **…** and italic as _…_.</summary>
    private static string TextOf(ParagraphElements elements)
    {
        var text = new StringBuilder();
        foreach (var element in elements)
        {
            switch (element)
            {
                case Text t:
                    text.Append(t.Content);
                    break;
                case FormattedText f:
                    var inner = TextOf(f.Elements);
                    text.Append(f.Bold == true ? $"**{inner}**" : f.Italic == true ? $"_{inner}_" : inner);
                    break;
                case Character { SymbolName: SymbolName.LineBreak }:
                    text.Append('\n');
                    break;
                case Character { SymbolName: SymbolName.Tab }:
                    text.Append('\t');
                    break;
                case Character c:
                    text.Append(c.Char);
                    break;
            }
        }

        return text.ToString();
    }

    private static string TextOf(Paragraph paragraph) => TextOf(paragraph.Elements);

    private static List<Paragraph> Paragraphs(Section section) => section.Elements.OfType<Paragraph>().ToList();

    [Test]
    public void Render_MakesAnA4Pdf_TitledButNeverNamingThePatient_InTheBundledFont()
    {
        var bytes = PdfSharpLetterRenderer.Render(Letter());

        Encoding.ASCII.GetString(bytes, 0, 5).Should().Be("%PDF-");
        using var pdf = PdfReader.Open(new MemoryStream(bytes), PdfDocumentOpenMode.Import);
        pdf.PageCount.Should().Be(1);
        pdf.Pages[0].Width.Point.Should().BeApproximately(595, 1);
        pdf.Info.Title.Should().Be("Clinic letter");
        Encoding.Latin1.GetString(bytes).Should().Contain("Atkinson").And.Contain("pdfaid:part").And.NotContain("Sam Example");
    }

    [Test]
    public void Render_ALongBody_RunsOntoMorePages_AndLargePrintOntoMoreStill()
    {
        var body = string.Join("\n\n", Enumerable.Range(1, 60).Select(i => $"Paragraph {i} with enough words to fill a line or so."));

        var standard = PdfReader.Open(new MemoryStream(PdfSharpLetterRenderer.Render(Letter(body))), PdfDocumentOpenMode.Import).PageCount;
        var large = PdfReader.Open(new MemoryStream(PdfSharpLetterRenderer.Render(Letter(body, largePrint: true))), PdfDocumentOpenMode.Import).PageCount;

        standard.Should().BeGreaterThan(1);
        large.Should().BeGreaterThan(standard);
    }

    [Test]
    public void Render_WithoutAMarking_And_CharactersTheFontLacks_StillRenders()
    {
        var bytes = PdfSharpLetterRenderer.Render(Letter("Zoë Łukasz 中文 ✓ 😀", marking: null));

        PdfReader.Open(new MemoryStream(bytes), PdfDocumentOpenMode.Import).PageCount.Should().Be(1);
    }

    [Test]
    public async Task RenderAsync_RendersIt_AndFreesItsPlace()
    {
        var bytes = await new PdfSharpLetterRenderer().RenderAsync(Letter(), CancellationToken.None);

        bytes.Should().NotBeEmpty();
        PdfSharpLetterRenderer.Available.Should().Be(PdfSharpLetterRenderer.MaxConcurrent);
    }

    [Test]
    public async Task RenderAsync_Cancelled_RendersNothing()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = () => new PdfSharpLetterRenderer().RenderAsync(Letter(), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        PdfSharpLetterRenderer.Available.Should().Be(PdfSharpLetterRenderer.MaxConcurrent);
    }

    [Test]
    public void FindUnsupportedCharacters_ListsWhatTheFontLacks_EachOnce()
    {
        var renderer = new PdfSharpLetterRenderer();

        renderer.FindUnsupportedCharacters("Zoë Łukasz – “quoted” £5 €\n\tO'Brien").Should().BeEmpty();
        renderer.FindUnsupportedCharacters("中文 ✓ 中 😀").Should().Equal("中", "文", "✓", "😀");
        renderer.FindUnsupportedCharacters(null).Should().BeEmpty();
        renderer.FindUnsupportedCharacters("").Should().BeEmpty();
    }

    [Test]
    public void Build_LaysOutEveryPart_WithTheMarkingAndIdentifiersOnEveryPage()
    {
        var document = PdfSharpLetterRenderer.Build(Letter());
        var section = document.Sections[0];
        var paragraphs = Paragraphs(section);

        section.PageSetup.PageFormat.Should().Be(PageFormat.A4);
        document.Styles[StyleNames.Normal]!.Font.Name.Should().Be(LetterFonts.FamilyName);
        document.Styles[StyleNames.Normal]!.Font.Size.Point.Should().Be(11);
        TextOf(paragraphs[0]).Should().Be("**Synthetic Specialist Centre**\n1 Imaginary Lane");
        TextOf(paragraphs[1]).Should().Be("Testbrook Surgery\nFictionton");
        TextOf(paragraphs[2]).Should().Be("Dear Doctor,");
        TextOf(paragraphs[3]).Should().Be("Yours sincerely,\n\nAlex Author");
        TextOf(section.Headers.Primary.Elements.OfType<Paragraph>().Single()).Should().Be("Draft, not approved");
        var footer = section.Footers.Primary.Elements.OfType<Paragraph>().ToList();
        TextOf(footer[0]).Should().Be(Identifiers);
        TextOf(footer[1]).Should().Be("**Draft, not approved** · Page  of ");
        footer[1].Elements.OfType<PageField>().Should().ContainSingle();
        footer[1].Elements.OfType<NumPagesField>().Should().ContainSingle();
    }

    [Test]
    public void Build_LargePrint_Is16Point_AndNoMarkingOrAddressLeavesThemOut()
    {
        var document = PdfSharpLetterRenderer.Build(Letter(largePrint: true, marking: null) with { RecipientAddress = [], Closing = [] });
        var section = document.Sections[0];

        document.Styles[StyleNames.Normal]!.Font.Size.Point.Should().Be(16);
        document.Styles[StyleNames.Footer]!.Font.Size.Point.Should().Be(16);
        section.Headers.Primary.Elements.Count.Should().Be(0);
        Paragraphs(section).Select(TextOf).Should().Equal("**Synthetic Specialist Centre**\n1 Imaginary Lane", "Dear Doctor,");
        TextOf(section.Footers.Primary.Elements.OfType<Paragraph>().Last()).Should().Be("Page  of ");
    }

    [Test]
    public void Markdown_HeadingsParagraphsAndEmphasis()
    {
        var section = new Document().AddSection();

        MarkdownToMigraDoc.AddTo(section, "# One\n\n## Two\n\n#### Four\n\nSome **bold**, _italic_ and `code`,\nwrapped.  \nBroken\\\nagain.");
        var paragraphs = Paragraphs(section);

        paragraphs.Select(p => p.Style).Should().Equal(StyleNames.Heading1, StyleNames.Heading2, StyleNames.Heading3, "");
        paragraphs.Select(TextOf).Should().Equal("One", "Two", "Four", "Some **bold**, _italic_ and code, wrapped.\nBroken\nagain.");
    }

    [Test]
    public void Markdown_ListsHangTheirMarkers_AndNest()
    {
        var section = new Document().AddSection();

        MarkdownToMigraDoc.AddTo(section, "3. Three\n4. Four\n   - Nested\n\n     More of it\n\n* Bullet");
        var paragraphs = Paragraphs(section);

        paragraphs.Select(TextOf).Should().Equal("3.\tThree", "4.\tFour", "•\tNested", "More of it", "•\tBullet");
        paragraphs[0].Format.LeftIndent.Should().Be(MarkdownToMigraDoc.Indent);
        paragraphs[0].Format.FirstLineIndent.Should().Be(-MarkdownToMigraDoc.Indent);
        paragraphs[2].Format.LeftIndent.Should().Be(MarkdownToMigraDoc.Indent * 2);
        paragraphs[3].Format.LeftIndent.Should().Be(MarkdownToMigraDoc.Indent * 2);
    }

    [Test]
    public void Markdown_QuotesRulesCodeLinksAndHtml_AreShownAsText()
    {
        var section = new Document().AddSection();

        MarkdownToMigraDoc.AddTo(section,
            "> Quoted\n\n---\n\n```\nline 1\nline 2\n```\n\n[a link](https://example.invalid) <https://example.invalid/x> " +
            "![a picture](https://example.invalid/p.png) <b>bold?</b> &amp;\n\n[ref]: https://example.invalid/ref\n\n<div>block</div>");
        var paragraphs = Paragraphs(section);

        paragraphs.Select(TextOf).Should().Equal(
            "Quoted",
            "",
            "line 1\nline 2",
            "a link (https://example.invalid) https://example.invalid/x a picture <b>bold?</b> &",
            "<div>block</div>");
        paragraphs[0].Format.LeftIndent.Should().Be(MarkdownToMigraDoc.Indent);
        paragraphs[1].Format.Borders.Bottom.Width.Point.Should().Be(0.75);
    }

    [Test]
    public void Markdown_Nothing_AddsNothing()
    {
        var section = new Document().AddSection();

        MarkdownToMigraDoc.AddTo(section, "");

        section.Elements.Count.Should().Be(0);
    }

    [Test]
    public void Markdown_ThousandsOfNestedQuotes_AreShownAsTheirPlainText_WithoutThrowing()
    {
        var section = new Document().AddSection();
        var markdown = new string('>', 5000) + " Deep";

        MarkdownToMigraDoc.AddTo(section, markdown);

        Paragraphs(section).Select(TextOf).Should().Equal(markdown);
    }

    [Test]
    public void Markdown_DozensOfNestedQuotes_StopIndentingAtTheCap()
    {
        var section = new Document().AddSection();

        MarkdownToMigraDoc.AddTo(section, new string('>', 60) + " Deep");
        var paragraphs = Paragraphs(section);

        paragraphs.Select(TextOf).Should().Equal("Deep");
        paragraphs[0].Format.LeftIndent.Should().Be(MarkdownToMigraDoc.Indent * MarkdownToMigraDoc.MaxDepth);
    }

    [Test]
    public void Markdown_ThousandsOfNestedListItems_RenderWithoutThrowing_WithinTheCap()
    {
        var section = new Document().AddSection();
        var markdown = string.Concat(Enumerable.Repeat("- ", 5000)) + "Deep";

        MarkdownToMigraDoc.AddTo(section, markdown);
        var paragraphs = Paragraphs(section);

        paragraphs.Select(TextOf).Should().Contain(t => t.EndsWith("Deep"));
        paragraphs.Should().OnlyContain(p =>
            p.Format.LeftIndent.Point <= (MarkdownToMigraDoc.Indent * MarkdownToMigraDoc.MaxDepth).Point + 0.01);
    }

    [Test]
    public void Markdown_NestedListsWithText_IndentUpToTheCap()
    {
        var section = new Document().AddSection();
        var markdown = string.Join("\n", Enumerable.Range(0, 12).Select(i => new string(' ', i * 2) + $"- Level {i}"));

        MarkdownToMigraDoc.AddTo(section, markdown);
        var paragraphs = Paragraphs(section);

        paragraphs.Select(TextOf).Should().Equal(Enumerable.Range(0, 12).Select(i => i <= MarkdownToMigraDoc.MaxDepth ? $"•\tLevel {i}" : $"Level {i}"));
        paragraphs[^1].Format.LeftIndent.Should().Be(MarkdownToMigraDoc.Indent * MarkdownToMigraDoc.MaxDepth);
    }

    [Test]
    public void Markdown_DeeplyNestedEmphasis_IsShownAsPlainTextPastTheCap()
    {
        var section = new Document().AddSection();
        var open = string.Concat(Enumerable.Range(0, 40).Select(i => i % 2 == 0 ? "*a " : "_b "));
        var close = string.Concat(Enumerable.Range(0, 40).Reverse().Select(i => i % 2 == 0 ? " a*" : " b_"));

        MarkdownToMigraDoc.AddTo(section, open + "deep" + close);
        var paragraph = Paragraphs(section).Single();

        TextOf(paragraph).Should().Contain("deep");
        FormattingDepth(paragraph.Elements).Should().BeLessThanOrEqualTo(MarkdownToMigraDoc.MaxInlineDepth + 1);
    }

    private static int FormattingDepth(ParagraphElements elements) =>
        elements.OfType<FormattedText>().Select(f => 1 + FormattingDepth(f.Elements)).DefaultIfEmpty(0).Max();

    [Test]
    public void Render_AFullBodyOfNestedQuotes_StillRenders()
    {
        var bytes = PdfSharpLetterRenderer.Render(Letter(new string('>', 19990) + " Deep"));

        PdfReader.Open(new MemoryStream(bytes), PdfDocumentOpenMode.Import).PageCount.Should().Be(1);
    }

    [Test]
    public void Fonts_ResolveEveryFamilyToTheBundledFaces()
    {
        LetterFonts.Instance.ResolveTypeface("Arial", bold: true, italic: true).FaceName.Should().Be("AtkinsonHyperlegible-BoldItalic");
        LetterFonts.Instance.ResolveTypeface(LetterFonts.FamilyName, false, true).FaceName.Should().Be("AtkinsonHyperlegible-Italic");
        LetterFonts.Instance.ResolveTypeface(LetterFonts.FamilyName, true, false).FaceName.Should().Be("AtkinsonHyperlegible-Bold");
        LetterFonts.Instance.ResolveTypeface(LetterFonts.FamilyName, false, false).FaceName.Should().Be("AtkinsonHyperlegible-Regular");
        foreach (var face in new[] { "Regular", "Bold", "Italic", "BoldItalic" })
            LetterFonts.Instance.GetFont($"AtkinsonHyperlegible-{face}")!.Take(4).Should().Equal(0, 1, 0, 0);
        LetterFonts.Instance.GetFont("Arial").Should().BeNull();
    }

    [Test]
    public void Fonts_InstallTwice_IsHarmless()
    {
        LetterFonts.Install();
        LetterFonts.Install();

        PdfSharp.Fonts.GlobalFontSettings.FontResolver.Should().BeSameAs(LetterFonts.Instance);
    }
}
