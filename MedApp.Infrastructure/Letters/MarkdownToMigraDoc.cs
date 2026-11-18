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
/// <para>
/// <b>Nesting is capped</b> (<see cref="MaxDepth"/> levels of quotes and lists, <see cref="MaxInlineDepth"/> of
/// emphasis and links): a body is at most 20,000 characters, but <c>&gt;&gt;&gt;…</c> or <c>- - - …</c> could nest
/// thousands deep, and following that by recursion could overflow the stack and end the API's process, or indent text
/// off the page. Markdig itself refuses input nested hundreds deep (shown as its plain lines). Deeper blocks are walked
/// without recursion and shown as plain paragraphs at the cap's indent; deeper
/// inline formatting as its plain text.
/// </para>
/// </summary>
public static class MarkdownToMigraDoc
{
    /// <summary>The paragraph styles it uses, which the renderer defines.</summary>
    public const string Heading1 = StyleNames.Heading1;

    public const string Heading2 = StyleNames.Heading2;
    public const string Heading3 = StyleNames.Heading3;
    public const string Quote = "Quote";

    /// <summary>The most levels of quotes and lists indented and followed by recursion.</summary>
    public const int MaxDepth = 8;

    /// <summary>The most levels of emphasis and links followed by recursion.</summary>
    public const int MaxInlineDepth = 16;

    /// <summary>How far each level of list or quote is indented.</summary>
    public static readonly Unit Indent = Unit.FromCentimeter(0.75);

    /// <summary>CommonMark, with HTML switched off: tags in the text stay text.</summary>
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().DisableHtml().Build();

    /// <summary>Adds the body's blocks to a section.</summary>
    public static void AddTo(Section section, string markdown)
    {
        MarkdownDocument document;
        try
        {
            document = Markdown.Parse(markdown ?? string.Empty, Pipeline);
        }
        // Markdig refuses input nested past its own limit (hundreds deep): show it as the plain lines it is instead.
        catch (ArgumentException)
        {
            AddPlainLines(section, markdown ?? string.Empty);
            return;
        }

        foreach (var block in document)
            AddBlock(section, block, 0);
    }

    private static void AddPlainLines(Section section, string text)
    {
        foreach (var block in text.Replace("\r\n", "\n").Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
        {
            var paragraph = section.AddParagraph();
            var lines = block.Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                if (i > 0)
                    paragraph.AddLineBreak();
                paragraph.AddText(lines[i]);
            }
        }
    }

    private static void AddBlock(Section section, Block block, int depth)
    {
        // Past the cap, the rest of this block is laid out flat, without recursion.
        if (depth > MaxDepth && block is ContainerBlock)
        {
            AddFlattened(section, block);
            return;
        }

        switch (block)
        {
            case HeadingBlock heading:
                var title = section.AddParagraph();
                title.Style = heading.Level switch { 1 => Heading1, 2 => Heading2, _ => Heading3 };
                Indented(title, depth);
                AddInlines(title.Elements, heading.Inline, 0);
                break;

            case ParagraphBlock paragraphBlock:
                var paragraph = section.AddParagraph();
                Indented(paragraph, depth);
                AddInlines(paragraph.Elements, paragraphBlock.Inline, 0);
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

    /// <summary>
    /// A block nested past the cap: every paragraph, heading and line inside it, in order, at the cap's indent, found
    /// with a stack rather than by recursion (list markers are left out).
    /// </summary>
    private static void AddFlattened(Section section, Block block)
    {
        var pending = new Stack<Block>();
        pending.Push(block);
        while (pending.Count > 0)
        {
            var next = pending.Pop();
            if (next is ContainerBlock container)
            {
                for (var i = container.Count - 1; i >= 0; i--)
                    pending.Push(container[i]);
            }
            else
            {
                AddBlock(section, next, MaxDepth);
            }
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
                    Indented(paragraph, Math.Min(depth + 1, MaxDepth));
                    paragraph.Format.FirstLineIndent = -Indent;
                    paragraph.Format.TabStops.ClearAll();
                    paragraph.Format.TabStops.AddTabStop(Indent * Math.Min(depth + 1, MaxDepth));
                    paragraph.Format.SpaceAfter = Unit.FromPoint(2);
                    paragraph.AddText(marker);
                    paragraph.AddTab();
                    AddInlines(paragraph.Elements, paragraphBlock.Inline, 0);
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
            paragraph.Format.LeftIndent = Indent * Math.Min(depth, MaxDepth);
    }

    private static void AddInlines(ParagraphElements elements, ContainerInline? container, int depth)
    {
        if (container is null)
            return;

        // Past the cap, the rest is its plain text, found without recursion.
        if (depth > MaxInlineDepth)
        {
            elements.AddText(PlainText(container));
            return;
        }

        foreach (var inline in container)
            AddInline(elements, inline, depth + 1);
    }

    /// <summary>The literal text inside an inline, in order, found with a stack rather than by recursion.</summary>
    private static string PlainText(ContainerInline container)
    {
        var text = new System.Text.StringBuilder();
        var pending = new Stack<Inline>();
        for (var inline = container.LastChild; inline is not null; inline = inline.PreviousSibling)
            pending.Push(inline);
        while (pending.Count > 0)
        {
            switch (pending.Pop())
            {
                case LiteralInline literal:
                    text.Append(literal.Content.ToString());
                    break;
                case CodeInline code:
                    text.Append(code.Content);
                    break;
                case LineBreakInline:
                    text.Append(' ');
                    break;
                case ContainerInline inner:
                    for (var child = inner.LastChild; child is not null; child = child.PreviousSibling)
                        pending.Push(child);
                    break;
            }
        }

        return text.ToString();
    }

    private static void AddInline(ParagraphElements elements, Inline inline, int depth)
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

                AddInlines(formatted.Elements, emphasis, depth);
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
                AddInlines(elements, image, depth);
                break;

            case LinkInline link:
                AddInlines(elements, link, depth);
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
                AddInlines(elements, container, depth);
                break;
        }
    }
}
