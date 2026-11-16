using System.Globalization;
using System.Text;

namespace MedApp.Application.Attachments.Common;

/// <summary>
/// File names as they're kept and served. A name is free text the uploader's computer chose, so it's cleaned before
/// it's stored: no folders, no control characters, and no Unicode direction controls (which can make
/// <c>invoice‮fdp.exe</c> display as <c>invoiceexe.pdf</c>).
/// </summary>
public static class AttachmentFileName
{
    /// <summary>Characters Windows doesn't allow in a file name, replaced in a download's name.</summary>
    private static readonly char[] InvalidOnWindows = ['<', '>', ':', '"', '/', '\\', '|', '?', '*'];

    /// <summary>
    /// The name to keep: normalised (NFC), without any folder, control or direction-control characters, trimmed of
    /// spaces and trailing dots, and shortened to <see cref="AttachmentLimits.MaxFileNameLength"/> keeping its
    /// extension. Null if nothing's left.
    /// </summary>
    public static string? Clean(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        var normalised = name.Normalize(NormalizationForm.FormC);
        var lastSeparator = normalised.LastIndexOfAny(['/', '\\']);
        if (lastSeparator >= 0)
            normalised = normalised[(lastSeparator + 1)..];

        var kept = new StringBuilder(normalised.Length);
        foreach (var c in normalised)
        {
            if (!char.IsControl(c) && !IsDirectionControl(c))
                kept.Append(c);
        }

        var cleaned = kept.ToString().Trim().TrimEnd('.', ' ');
        if (cleaned.Length == 0)
            return null;

        return Shorten(cleaned);
    }

    /// <summary>The extension, lower case with its dot (".pdf"), or empty if it has none.</summary>
    public static string Extension(string name)
    {
        var dot = name.LastIndexOf('.');
        return dot <= 0 || dot == name.Length - 1 ? string.Empty : name[dot..].ToLowerInvariant();
    }

    /// <summary>
    /// The name to give a download: the kept name with any character Windows refuses replaced, so the browser saves
    /// it as it is. Falls back to "file" plus the type's usual extension.
    /// </summary>
    public static string ForDownload(string fileName, AttachmentFileType? type)
    {
        var name = Clean(fileName) ?? string.Empty;
        foreach (var c in InvalidOnWindows)
            name = name.Replace(c, '_');

        return name.Length > 0 && Extension(name).Length > 0
            ? name
            : "file" + (type?.Extensions[0] ?? string.Empty);
    }

    /// <summary>The name in plain ASCII, for browsers that don't read <c>filename*</c>: anything else becomes "_".</summary>
    public static string AsciiFallback(string name)
    {
        var ascii = new StringBuilder(name.Length);
        foreach (var c in name)
            ascii.Append(c is >= ' ' and <= '~' and not '"' and not '\\' ? c : '_');
        return ascii.ToString();
    }

    /// <summary>Keeps the extension and cuts the rest, on a whole character (never half a surrogate pair).</summary>
    private static string Shorten(string name)
    {
        if (name.Length <= AttachmentLimits.MaxFileNameLength)
            return name;

        var extension = Extension(name);
        if (extension.Length >= AttachmentLimits.MaxFileNameLength / 2)
            extension = string.Empty;

        var room = AttachmentLimits.MaxFileNameLength - extension.Length;
        var stem = new StringBuilder(room);
        var elements = StringInfo.GetTextElementEnumerator(name[..^extension.Length]);
        while (elements.MoveNext() && stem.Length + elements.GetTextElement().Length <= room)
            stem.Append(elements.GetTextElement());

        return stem.ToString().TrimEnd('.', ' ') + extension;
    }

    /// <summary>Unicode's explicit direction controls (U+202A–U+202E, U+2066–U+2069) and the marks U+200E, U+200F, U+061C.</summary>
    private static bool IsDirectionControl(char c) =>
        c is >= '‪' and <= '‮' or >= '⁦' and <= '⁩' or '‎' or '‏' or '؜';
}
