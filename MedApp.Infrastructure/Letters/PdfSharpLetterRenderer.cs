using MedApp.Application.Letters.Common;
using MigraDoc.DocumentObjectModel;
using MigraDoc.Rendering;
using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Events;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using PdfSharp.UniversalAccessibility;

namespace MedApp.Infrastructure.Letters;

/// <summary>
/// Renders letters as A4 PDFs with MigraDoc and PDFsharp (MIT licence, fully managed: no native library), in
/// Atkinson Hyperlegible (<see cref="LetterFonts"/>), embedded. Decision 98 of docs/centre-phases-3-5-plan.md.
/// <list type="bullet">
/// <item><b>Every page</b> carries the patient's identifiers and "Page n of m" in its footer (hazard H-50), and the
/// marking, if any ("Draft, not approved"), in its header and footer.</item>
/// <item><b>The first page</b> starts with the letterhead (right-aligned, its first line bold), then the recipient's
/// address; then the body (Markdown, <see cref="MarkdownToMigraDoc"/>) and the closing.</item>
/// <item><b>Large print</b> is 16 point throughout, headings larger.</item>
/// <item><b>Tagged, in PDFsharp's PDF/A-1a mode</b> (fonts embedded and subset, an sRGB output intent, XMP metadata, a
/// structure tree, language en-GB, the title shown instead of the file name). The tags are as fine as PDFsharp allows
/// for a page MigraDoc lays out: each page's text is one paragraph in reading order, the marking another, and the footer
/// page furniture (an artifact). veraPDF 1.30 passes a letter as PDF/A-1a (and 1b) while every character is in the
/// Windows-1252 set; one with other characters (Ł, ł, ş, ğ: names in Polish or Turkish) fails clause 6.3.5, because
/// PDFsharp 6.2 writes no CIDSet for the font it then adds. So MedApp doesn't claim PDF/A yet (docs/todo.md).</item>
/// <item><b>Characters the font lacks</b> are drawn as "?", never as a missing glyph (which PDF/A forbids, and which
/// prints as an empty box); <see cref="FindUnsupportedCharacters"/> lets callers refuse them first.</item>
/// <item><b>Limited:</b> at most <see cref="MaxConcurrent"/> letters render at a time in each copy of the API, as each
/// holds its whole layout in memory; others wait. A body is at most 20,000 characters, so each is bounded.</item>
/// </list>
/// The PDF's title is the letter's kind or template name, never the patient's.
/// </summary>
public sealed class PdfSharpLetterRenderer : ILetterRenderer
{
    public const int MaxConcurrent = 2;

    /// <summary>What a character the font lacks is drawn as.</summary>
    public const char Replacement = '?';

    private static readonly SemaphoreSlim Gate = new(MaxConcurrent, MaxConcurrent);

    /// <summary>How many places are free now (for tests).</summary>
    public static int Available => Gate.CurrentCount;

    public async Task<byte[]> RenderAsync(LetterDocument letter, CancellationToken ct)
    {
        await Gate.WaitAsync(ct);
        try
        {
            ct.ThrowIfCancellationRequested();
            return Render(letter);
        }
        finally
        {
            Gate.Release();
        }
    }

    public IReadOnlyList<string> FindUnsupportedCharacters(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return [];

        LetterFonts.Install();
        var missing = new List<string>();
        var events = new RenderEvents();
        events.RenderTextEvent += (_, e) =>
        {
            foreach (var pair in e.CodePointGlyphIndexPairs)
            {
                if (pair.GlyphIndex != 0 || IsLayout(pair.CodePoint))
                    continue;
                var character = char.ConvertFromUtf32(pair.CodePoint);
                if (!missing.Contains(character))
                    missing.Add(character);
            }
        };

        var measure = XGraphics.CreateMeasureContext(new XSize(1000, 1000), XGraphicsUnit.Point, XPageDirection.Downwards, events);
        foreach (var face in Faces)
            measure.MeasureString(text, face);
        return missing;
    }

    /// <summary>The four faces, at any size: a character missing from any of them is unsupported.</summary>
    private static readonly XFont[] Faces =
    [
        new(LetterFonts.FamilyName, 10, XFontStyleEx.Regular),
        new(LetterFonts.FamilyName, 10, XFontStyleEx.Bold),
        new(LetterFonts.FamilyName, 10, XFontStyleEx.Italic),
        new(LetterFonts.FamilyName, 10, XFontStyleEx.BoldItalic),
    ];

    /// <summary>Spaces, line breaks and other controls: they're laid out, never drawn as glyphs.</summary>
    private static bool IsLayout(int codePoint) =>
        codePoint <= char.MaxValue && (char.IsControl((char)codePoint) || char.IsWhiteSpace((char)codePoint));

    /// <summary>The letter's PDF, synchronously and without the limit (for tests and the limit's own use).</summary>
    public static byte[] Render(LetterDocument letter)
    {
        LetterFonts.Install();

        var document = Build(letter);
        var pdf = new PdfDocument();
        pdf.Info.Title = letter.Title;
        pdf.Info.Creator = "MedApp";
        pdf.ViewerPreferences.DisplayDocTitle = true;
        pdf.RenderEvents.RenderTextEvent += (_, e) => ReplaceMissingGlyphs(e);

        // PDFsharp's PDF/A mode, which also tags what's drawn, so every drawing must be inside a structure element.
        pdf.SetPdfA();
        var accessibility = UAManager.ForDocument(pdf);
        accessibility.SetDocumentLanguage(Language);
        var structure = accessibility.StructureBuilder;

        var layout = new DocumentRenderer(document);
        layout.PrepareDocument(pdf.RenderEvents);
        var pageCount = layout.FormattedDocument?.PageCount
                        ?? throw new InvalidOperationException("MigraDoc laid out no document.");
        for (var number = 1; number <= pageCount; number++)
        {
            var page = pdf.AddPage();
            page.Size = PageSize.A4;
            using var graphics = XGraphics.FromPdfPage(page);

            // The marking ("Draft, not approved") is read out on every page.
            if (letter.Marking is not null)
            {
                structure.BeginElement(PdfBlockLevelElementTag.Paragraph);
                layout.RenderPage(graphics, number, PageRenderOptions.RenderHeader);
                structure.End();
            }

            // The footer's identifiers and page numbers are page furniture, as a screen reader expects; they're printed
            // on every page.
            structure.BeginArtifact();
            layout.RenderPage(graphics, number, PageRenderOptions.RenderFooter);
            structure.End();

            // MigraDoc lays the page out as one piece, so its content is one element, in reading order.
            structure.BeginElement(PdfBlockLevelElementTag.Paragraph);
            layout.RenderPage(graphics, number, PageRenderOptions.RenderContent);
            structure.End();
        }

        using var stream = new MemoryStream();
        pdf.Save(stream, closeStream: false);
        return stream.ToArray();
    }

    /// <summary>Draws (and measures) a character the font lacks as <see cref="Replacement"/>.</summary>
    private static void ReplaceMissingGlyphs(RenderTextEventArgs e)
    {
        if (e.CodePointGlyphIndexPairs is not { } pairs)
            return;
        for (var i = 0; i < pairs.Length; i++)
        {
            if (pairs[i].GlyphIndex != 0 || IsLayout(pairs[i].CodePoint))
                continue;
            pairs[i] = new CodePointGlyphIndexPair(Replacement, 0);
            e.ReevaluateGlyphIndices = true;
        }
    }

    /// <summary>The language every letter is in.</summary>
    public const string Language = "en-GB";

    /// <summary>The MigraDoc document a letter is laid out as (for tests).</summary>
    public static Document Build(LetterDocument letter)
    {
        var sizes = letter.LargePrint ? Sizes.Large : Sizes.Standard;
        var document = new Document();
        document.Info.Title = letter.Title;
        DefineStyles(document, sizes);

        var section = document.AddSection();
        var page = section.PageSetup;
        page.PageFormat = PageFormat.A4;
        page.TopMargin = Unit.FromCentimeter(letter.Marking is null ? 2 : 2.8);
        page.BottomMargin = Unit.FromCentimeter(letter.LargePrint ? 3.2 : 2.5);
        page.LeftMargin = Unit.FromCentimeter(2.2);
        page.RightMargin = Unit.FromCentimeter(2.2);
        page.HeaderDistance = Unit.FromCentimeter(1);
        page.FooterDistance = Unit.FromCentimeter(1);

        if (letter.Marking is { } marking)
        {
            var header = section.Headers.Primary.AddParagraph();
            header.Style = MarkingStyle;
            header.AddText(marking);
        }

        AddFooter(section, letter);

        AddLines(section, letter.Letterhead, LetterheadStyle, boldFirst: true);
        if (letter.RecipientAddress.Count > 0)
            AddLines(section, letter.RecipientAddress, AddressStyle, boldFirst: false);

        MarkdownToMigraDoc.AddTo(section, letter.Body);

        if (letter.Closing.Count > 0)
            AddLines(section, letter.Closing, ClosingStyle, boldFirst: false);

        return document;
    }

    private const string MarkingStyle = "Marking";
    private const string LetterheadStyle = "Letterhead";
    private const string AddressStyle = "Address";
    private const string ClosingStyle = "Closing";

    private sealed record Sizes(double Body, double Heading1, double Heading2, double Heading3, double Footer)
    {
        public static readonly Sizes Standard = new(11, 16, 13.5, 12, 9);

        /// <summary>16 point throughout, for the Accessible Information Standard.</summary>
        public static readonly Sizes Large = new(16, 22, 19, 17, 16);
    }

    private static void DefineStyles(Document document, Sizes sizes)
    {
        var normal = document.Styles[StyleNames.Normal]!;
        normal.Font.Name = LetterFonts.FamilyName;
        normal.Font.Size = sizes.Body;
        normal.Font.Color = Colors.Black;
        normal.ParagraphFormat.SpaceAfter = Unit.FromPoint(sizes.Body * 0.6);
        normal.ParagraphFormat.LineSpacingRule = LineSpacingRule.OnePtFive;

        Heading(document, StyleNames.Heading1, sizes.Heading1);
        Heading(document, StyleNames.Heading2, sizes.Heading2);
        Heading(document, StyleNames.Heading3, sizes.Heading3);

        var marking = document.Styles.AddStyle(MarkingStyle, StyleNames.Normal);
        marking.Font.Bold = true;
        marking.ParagraphFormat.Alignment = ParagraphAlignment.Center;
        marking.ParagraphFormat.SpaceAfter = 0;
        marking.ParagraphFormat.Borders.Width = 1;
        marking.ParagraphFormat.Borders.Color = Colors.Black;
        marking.ParagraphFormat.Borders.Distance = Unit.FromPoint(3);

        var letterhead = document.Styles.AddStyle(LetterheadStyle, StyleNames.Normal);
        letterhead.ParagraphFormat.Alignment = ParagraphAlignment.Right;
        letterhead.ParagraphFormat.SpaceAfter = 0;
        letterhead.ParagraphFormat.LineSpacingRule = LineSpacingRule.Single;

        var address = document.Styles.AddStyle(AddressStyle, StyleNames.Normal);
        address.ParagraphFormat.SpaceBefore = Unit.FromPoint(sizes.Body * 2);
        address.ParagraphFormat.SpaceAfter = Unit.FromPoint(sizes.Body * 2);
        address.ParagraphFormat.LineSpacingRule = LineSpacingRule.Single;

        var closing = document.Styles.AddStyle(ClosingStyle, StyleNames.Normal);
        closing.ParagraphFormat.SpaceBefore = Unit.FromPoint(sizes.Body);
        closing.ParagraphFormat.LineSpacingRule = LineSpacingRule.Single;
        closing.ParagraphFormat.KeepTogether = true;

        var footer = document.Styles[StyleNames.Footer]!;
        footer.Font.Size = sizes.Footer;
        footer.ParagraphFormat.SpaceAfter = 0;
        footer.ParagraphFormat.LineSpacingRule = LineSpacingRule.Single;

        var header = document.Styles[StyleNames.Header]!;
        header.Font.Size = sizes.Body;
    }

    private static void Heading(Document document, string name, double size)
    {
        var style = document.Styles[name]!;
        style.BaseStyle = StyleNames.Normal;
        style.Font.Size = size;
        style.Font.Bold = true;
        style.ParagraphFormat.SpaceBefore = Unit.FromPoint(size * 0.8);
        style.ParagraphFormat.SpaceAfter = Unit.FromPoint(size * 0.4);
        style.ParagraphFormat.KeepWithNext = true;
        style.ParagraphFormat.LineSpacingRule = LineSpacingRule.Single;
    }

    /// <summary>Below a rule, the patient's identifiers, then the marking and "Page n of m" against the right margin.</summary>
    private static void AddFooter(Section section, LetterDocument letter)
    {
        var identifiers = section.Footers.Primary.AddParagraph();
        identifiers.Style = StyleNames.Footer;
        identifiers.Format.Borders.Top.Width = 0.5;
        identifiers.Format.Borders.Top.Color = Colors.Black;
        identifiers.Format.Borders.Distance = Unit.FromPoint(3);
        identifiers.AddText(letter.PatientIdentifiers);

        var pages = section.Footers.Primary.AddParagraph();
        pages.Style = StyleNames.Footer;
        pages.Format.Alignment = ParagraphAlignment.Right;
        if (letter.Marking is { } marking)
        {
            var words = pages.AddFormattedText(marking);
            words.Bold = true;
            pages.AddText(" · ");
        }

        pages.AddText("Page ");
        pages.AddPageField();
        pages.AddText(" of ");
        pages.AddNumPagesField();
    }

    private static void AddLines(Section section, IReadOnlyList<string> lines, string style, bool boldFirst)
    {
        var paragraph = section.AddParagraph();
        paragraph.Style = style;
        for (var i = 0; i < lines.Count; i++)
        {
            if (i > 0)
                paragraph.AddLineBreak();
            if (i == 0 && boldFirst)
                paragraph.AddFormattedText(lines[i], TextFormat.Bold);
            else
                paragraph.AddText(lines[i]);
        }
    }
}
