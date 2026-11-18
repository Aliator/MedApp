using System.Globalization;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using MigraDoc.DocumentObjectModel;

namespace MedApp.Infrastructure.Letters;

/// <summary>
/// Turns a letter's Markdown body into MigraDoc paragraphs, with Markdig: headings (three sizes), paragraphs, bold and
/// italic, bulleted and numbered lists (nested), quotes (indented), rules, and line breaks. HTML is never interpreted,
/// only shown as the text it is, and so is anything else Markdown doesn't make a paragraph of (code is plain text; a
/// link is its text with its address after it, as it would have to be on paper). Images aren't loaded: their
/// description is shown.
/// </summary>
public static class MarkdownToMigraDoc
{
    /// <summary>The paragraph styles it uses, which the renderer defines.</summary>
    public const string Heading1 = StyleNames.Heading1;

    public const string Heading2 = StyleNames.Heading2;
    public const string Heading3 = StyleNames.Heading3;
    public const string Quote = "Quote";

    /// <summary>How far each level of list or quote is indented.</summary>
    public static readonly Unit Indent = Unit.FromCentimeter(0.75);

    /// <summary>CommonMark, with HTML switched off: tags in the text stay text.</summary>
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().DisableHtml().Build();

    /// <summary>Adds the body's blocks to a section.</summary>
    public static void AddTo(Section section, string markdown)
    {
        var document = Markdown.Parse(markdown ?? string.Empty, Pipeline);
        foreach (var block in document)
            AddBlock(section, block, 0);
    }

    private static void AddBlock(Section section, Block block, int depth)
    {
        switch (block)
        {
            case HeadingBlock heading:
                var title = section.AddParagraph();
                title.Style = heading.Level switch { 1 => Heading1, 2 => Heading2, _ => Heading3 };
                Indented(title, depth);
                AddInlines(title.Elements, heading.Inline);
                break;

            case ParagraphBlock paragraphBlock:
                var paragraph = section.AddParagraph();
                Indented(paragraph, depth);
                AddInlines(paragraph.Elements, paragraphBlock.Inline);
                break;

            case ListBlock list:
                AddList(section, list, depth);
                break;

            case QuoteBlock quote:
                foreach (var child in quote)
                    AddBlock(section, child, depth + 1);
                break;

            case ThematicBreakBlock:
                var rule = section.AddParagraph();
                Indented(rule, depth);
                rule.Format.Borders.Bottom.Width = 0.75;
                rule.Format.Borders.Bottom.Color = Colors.Black;
                break;

            // [label]: address lines define links' addresses; they're not text.
            case LinkReferenceDefinitionGroup or LinkReferenceDefinition:
                break;

            case LeafBlock leaf when leaf.Lines.Count > 0:
                // Code, and anything else made of lines: shown as the lines it is.
                var text = section.AddParagraph();
                Indented(text, depth);
                var lines = leaf.Lines.Lines.Take(leaf.Lines.Count).Select(l => l.Slice.ToString()).ToList();
                for (var i = 0; i < lines.Count; i++)
                {
                    if (i > 0)
                        text.AddLineBreak();
                    text.AddText(lines[i]);
                }

                break;

            case ContainerBlock container:
                foreach (var child in container)
                    AddBlock(section, child, depth);
                break;
        }
    }

    private static void AddList(Section section, ListBlock list, int depth)
    {
        var number = list.IsOrdered && int.TryParse(list.OrderedStart, NumberStyles.None, CultureInfo.InvariantCulture, out var start)
            ? start
            : 1;

        foreach (var item in list.OfType<ListItemBlock>())
        {
            var marker = list.IsOrdered ? $"{number++}." : "•";
            var first = true;
            foreach (var child in item)
            {
                if (first && child is ParagraphBlock paragraphBlock)
                {
                    // The item's first paragraph carries its marker, hanging in the indent.
                    var paragraph = section.AddParagraph();
                    Indented(paragraph, depth + 1);
                    paragraph.Format.FirstLineIndent = -Indent;
                    paragraph.Format.TabStops.ClearAll();
                    paragraph.Format.TabStops.AddTabStop(Indent * (depth + 1));
                    paragraph.Format.SpaceAfter = Unit.FromPoint(2);
                    paragraph.AddText(marker);
                    paragraph.AddTab();
                    AddInlines(paragraph.Elements, paragraphBlock.Inline);
                }
                else
                {
                    AddBlock(section, child, depth + 1);
                }

                first = false;
            }
        }
    }

    private static void Indented(Paragraph paragraph, int depth)
    {
        if (depth > 0)
            paragraph.Format.LeftIndent = Indent * depth;
    }

    private static void AddInlines(ParagraphElements elements, ContainerInline? container)
    {
        if (container is null)
            return;

        foreach (var inline in container)
            AddInline(elements, inline);
    }

    private static void AddInline(ParagraphElements elements, Inline inline)
    {
        switch (inline)
        {
            case LiteralInline literal:
                elements.AddText(literal.Content.ToString());
                break;

            case EmphasisInline emphasis:
                var formatted = elements.AddFormattedText();
                if (emphasis.DelimiterChar is '*' or '_')
                {
                    if (emphasis.DelimiterCount >= 2)
                        formatted.Bold = true;
                    else
                        formatted.Italic = true;
                }

                AddInlines(formatted.Elements, emphasis);
                break;

            case LineBreakInline lineBreak:
                if (lineBreak.IsHard)
                    elements.AddLineBreak();
                else
                    elements.AddText(" ");
                break;

            case CodeInline code:
                elements.AddText(code.Content);
                break;

            case AutolinkInline autolink:
                elements.AddText(autolink.Url);
                break;

            case LinkInline { IsImage: true } image:
                AddInlines(elements, image);
                break;

            case LinkInline link:
                AddInlines(elements, link);
                if (!string.IsNullOrWhiteSpace(link.Url))
                    elements.AddText($" ({link.Url})");
                break;

            case HtmlEntityInline entity:
                elements.AddText(entity.Transcoded.ToString());
                break;

            case HtmlInline html:
                elements.AddText(html.Tag);
                break;

            case ContainerInline container:
                AddInlines(elements, container);
                break;
        }
    }
}
