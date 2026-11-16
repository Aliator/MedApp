using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace MedApp.Application.Attachments.Common;

/// <summary>
/// Works out what a file really is from its contents (its "magic bytes"), never from its name or the type the browser
/// sent, then checks its name's extension agrees. Only <see cref="AttachmentFileTypes.All"/> pass; anything else is
/// refused with a message saying why, and SVG images and web pages (HTML), which a browser could run script in, are
/// refused by name whatever they're called.
/// <list type="bullet">
/// <item>PDF, PNG and JPEG by their signatures; HEIC and HEIF by their <c>ftyp</c> box's brands.</item>
/// <item>Word documents: a ZIP whose parts are a Word document's, read from its central directory without unpacking
/// it (only <c>[Content_Types].xml</c> is read, up to <see cref="MaxContentTypesBytes"/>). Macro-enabled ones are refused.</item>
/// <item>Text: anything else that's valid UTF-8 with no control characters but tabs, line breaks and form feeds,
/// and doesn't start with markup (<c>&lt;</c>).</item>
/// </list>
/// </summary>
public static class FileTypeSniffer
{
    /// <summary>How much of the start of a file the signatures and markup checks read.</summary>
    public const int HeaderBytes = 4096;

    /// <summary>The most of a Word document's <c>[Content_Types].xml</c> read (it's usually 1 or 2 KB).</summary>
    public const int MaxContentTypesBytes = 64 * 1024;

    private static readonly byte[] PdfSignature = "%PDF-"u8.ToArray();
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] ZipSignature = [0x50, 0x4B, 0x03, 0x04];
    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];

    private static readonly HashSet<string> HeicBrands = ["heic", "heix", "hevc", "hevx", "heim", "heis", "hevm", "hevs"];
    private static readonly HashSet<string> HeifBrands = ["mif1", "msf1"];
    private static readonly HashSet<string> AvifBrands = ["avif", "avis"];

    private const string WordMainContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml";

    /// <summary>
    /// The file's type, from <paramref name="content"/> (which must be seekable; it's read from the start and left
    /// wherever reading ended), checked against <paramref name="fileName"/>'s extension.
    /// </summary>
    /// <exception cref="UnsupportedFileTypeException">It's empty, not an allowed type, or its name doesn't match.</exception>
    public static AttachmentFileType Identify(Stream content, string fileName)
    {
        if (!content.CanSeek)
            throw new ArgumentException("The content must be seekable.", nameof(content));

        var type = IdentifyContent(content);
        var extension = AttachmentFileName.Extension(fileName);

        if (extension.Length == 0)
            throw new UnsupportedFileTypeException(
                $"The file's name has no extension. This file is a {type.Name}, so name it with {Describe(type.Extensions)}.");

        if (!type.Extensions.Contains(extension))
            throw new UnsupportedFileTypeException(
                $"The file's name ends in {extension}, but it's a {type.Name}. Name it with {Describe(type.Extensions)}.");

        return type;
    }

    /// <summary>The file's type from its contents alone (see <see cref="Identify"/>).</summary>
    public static AttachmentFileType IdentifyContent(Stream content)
    {
        content.Position = 0;
        var header = new byte[(int)Math.Min(HeaderBytes, content.Length)];
        content.ReadExactly(header);

        if (header.Length == 0)
            throw new UnsupportedFileTypeException("The file is empty.");

        RefuseMarkup(header);

        if (header.AsSpan().StartsWith(PdfSignature))
            return AttachmentFileTypes.Pdf;
        if (header.AsSpan().StartsWith(PngSignature))
            return AttachmentFileTypes.Png;
        if (header.AsSpan().StartsWith(JpegSignature))
            return AttachmentFileTypes.Jpeg;
        if (header.Length >= 12 && header.AsSpan(4, 4).SequenceEqual("ftyp"u8))
            return IdentifyIsoMedia(header);
        if (header.AsSpan().StartsWith(ZipSignature))
            return IdentifyWordDocument(content);

        if (IsText(content))
            return AttachmentFileTypes.Text;

        throw new UnsupportedFileTypeException(NotAllowed("This kind of file"));
    }

    /// <summary>
    /// SVG images and HTML pages, which browsers run script in, are refused by name; so is any other markup, which a
    /// browser might take for one. Markup is text whose first character, after a byte order mark and white space, is
    /// <c>&lt;</c>.
    /// </summary>
    private static void RefuseMarkup(byte[] header)
    {
        var start = header.AsSpan();
        if (start.StartsWith(Utf8Bom))
            start = start[Utf8Bom.Length..];

        var i = 0;
        while (i < start.Length && start[i] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n' or (byte)'\f')
            i++;

        if (i >= start.Length || start[i] != (byte)'<')
            return;

        var text = Encoding.Latin1.GetString(start[i..]).ToLowerInvariant();
        if (text.Contains("<svg"))
            throw new UnsupportedFileTypeException(NotAllowed("SVG images"));
        if (text.Contains("<html") || text.Contains("<!doctype html") || text.Contains("<head")
            || text.Contains("<body") || text.Contains("<script") || text.Contains("<iframe"))
            throw new UnsupportedFileTypeException(NotAllowed("Web pages (HTML)"));

        throw new UnsupportedFileTypeException(NotAllowed("Markup files (XML or HTML)"));
    }

    /// <summary>An ISO media file (its <c>ftyp</c> box): HEIC or HEIF images pass; AVIF, video and the rest don't.</summary>
    private static AttachmentFileType IdentifyIsoMedia(byte[] header)
    {
        var boxSize = (int)Math.Min(BinaryPrimitives.ReadUInt32BigEndian(header), (uint)header.Length);
        var brands = new List<string> { Encoding.ASCII.GetString(header, 8, 4) };
        for (var offset = 16; offset + 4 <= boxSize; offset += 4)
            brands.Add(Encoding.ASCII.GetString(header, offset, 4));

        if (brands.Any(AvifBrands.Contains))
            throw new UnsupportedFileTypeException(NotAllowed("AVIF images"));
        if (brands.Any(HeicBrands.Contains))
            return AttachmentFileTypes.Heic;
        if (brands.Any(HeifBrands.Contains))
            return AttachmentFileTypes.Heif;

        throw new UnsupportedFileTypeException(NotAllowed("Videos and other media files"));
    }

    /// <summary>
    /// A ZIP that's a Word document (.docx): it has a Word document's main part and declares it, and has no macros.
    /// Only the central directory and <c>[Content_Types].xml</c> are read, so a ZIP bomb is never unpacked.
    /// </summary>
    private static AttachmentFileType IdentifyWordDocument(Stream content)
    {
        content.Position = 0;
        try
        {
            using var zip = new ZipArchive(content, ZipArchiveMode.Read, leaveOpen: true);

            if (zip.GetEntry("word/vbaProject.bin") is not null)
                throw new UnsupportedFileTypeException(NotAllowed("Word documents with macros"));

            var contentTypes = zip.GetEntry("[Content_Types].xml");
            if (contentTypes is null || zip.GetEntry("word/document.xml") is null || contentTypes.Length > MaxContentTypesBytes)
                throw new UnsupportedFileTypeException(NotAllowed("ZIP files and other Office documents"));

            using var reader = new StreamReader(contentTypes.Open(), Encoding.UTF8);
            var buffer = new char[MaxContentTypesBytes];
            var read = reader.ReadBlock(buffer, 0, buffer.Length);
            var declared = new string(buffer, 0, read);

            if (declared.Contains("macroEnabled", StringComparison.OrdinalIgnoreCase))
                throw new UnsupportedFileTypeException(NotAllowed("Word documents with macros"));
            if (!declared.Contains(WordMainContentType, StringComparison.Ordinal))
                throw new UnsupportedFileTypeException(NotAllowed("ZIP files and other Office documents"));

            return AttachmentFileTypes.Docx;
        }
        catch (InvalidDataException)
        {
            throw new UnsupportedFileTypeException("The file looks like a Word document or ZIP file, but it's damaged.");
        }
    }

    /// <summary>
    /// The whole file is valid UTF-8 (a byte order mark allowed) with no control characters but tab, line feed,
    /// carriage return and form feed. Read in blocks, so a large file isn't held in memory.
    /// </summary>
    private static bool IsText(Stream content)
    {
        content.Position = 0;
        var decoder = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetDecoder();
        var bytes = new byte[16 * 1024];
        var chars = new char[bytes.Length + 1];

        try
        {
            int read;
            while ((read = content.Read(bytes, 0, bytes.Length)) > 0)
            {
                var count = decoder.GetChars(bytes, 0, read, chars, 0, flush: false);
                if (!AllPrintable(chars.AsSpan(0, count)))
                    return false;
            }

            var last = decoder.GetChars([], 0, 0, chars, 0, flush: true);
            return AllPrintable(chars.AsSpan(0, last));
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }

    private static bool AllPrintable(ReadOnlySpan<char> text)
    {
        foreach (var c in text)
        {
            if (c is '\t' or '\n' or '\r' or '\f')
                continue;
            if (char.IsControl(c))
                return false;
        }

        return true;
    }

    private static string NotAllowed(string what) =>
        $"{what} can't be attached. Attach {AttachmentFileTypes.AllowedText}.";

    private static string Describe(IReadOnlyList<string> extensions) =>
        extensions.Count == 1 ? extensions[0] : $"{string.Join(", ", extensions.Take(extensions.Count - 1))} or {extensions[^1]}";
}
