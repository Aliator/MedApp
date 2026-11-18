using PdfSharp.Fonts;

namespace MedApp.Infrastructure.Letters;

/// <summary>
/// The one font letters use, Atkinson Hyperlegible, from the four files embedded in this assembly
/// (<c>Letters/Fonts</c>; source and licence in its NOTICE.txt): the API's image has no system fonts, and PDFsharp's
/// cross-platform build reads none. Every family name resolves to it, so nothing a renderer asks for (a list bullet's
/// font, say) can fail or fall back to something missing.
/// </summary>
public sealed class LetterFonts : IFontResolver
{
    /// <summary>The family name letters' styles use.</summary>
    public const string FamilyName = "Atkinson Hyperlegible";

    private const string ResourcePrefix = "MedApp.Infrastructure.LetterFonts.";

    private static readonly Lock InstallLock = new();

    /// <summary>The one instance: PDFsharp wants the same resolver every time it's set.</summary>
    public static readonly LetterFonts Instance = new();

    private LetterFonts()
    {
    }

    /// <summary>
    /// Makes PDFsharp use these fonts, once per process (its font settings are global). Safe to call before every
    /// render, from any thread.
    /// </summary>
    public static void Install()
    {
        if (ReferenceEquals(GlobalFontSettings.FontResolver, Instance))
            return;

        lock (InstallLock)
        {
            if (!ReferenceEquals(GlobalFontSettings.FontResolver, Instance))
                GlobalFontSettings.FontResolver = Instance;
        }
    }

    /// <summary>The face for a family and style: always Atkinson Hyperlegible's, in the style asked for.</summary>
    public FontResolverInfo ResolveTypeface(string familyName, bool bold, bool italic) =>
        new(FaceName(bold, italic));

    /// <summary>A face's TrueType file, or null for a face that isn't one of the four.</summary>
    public byte[]? GetFont(string faceName)
    {
        if (faceName is not ("AtkinsonHyperlegible-Regular" or "AtkinsonHyperlegible-Bold"
            or "AtkinsonHyperlegible-Italic" or "AtkinsonHyperlegible-BoldItalic"))
            return null;

        using var stream = typeof(LetterFonts).Assembly.GetManifestResourceStream($"{ResourcePrefix}{faceName}.ttf")
                           ?? throw new InvalidOperationException($"The embedded font {faceName}.ttf is missing.");
        using var bytes = new MemoryStream();
        stream.CopyTo(bytes);
        return bytes.ToArray();
    }

    internal static string FaceName(bool bold, bool italic) => (bold, italic) switch
    {
        (true, true) => "AtkinsonHyperlegible-BoldItalic",
        (true, false) => "AtkinsonHyperlegible-Bold",
        (false, true) => "AtkinsonHyperlegible-Italic",
        _ => "AtkinsonHyperlegible-Regular",
    };
}
