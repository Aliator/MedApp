using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace MedApp.Application.Attachments.Common;

/// <summary>
/// Works out what a file really is from its contents (its "magic bytes"), never from its name or the type the browser
/// sent, then checks its name's extension agrees. Only <see cref="AttachmentFileTypes.All"/> pass; anything else is
/// refused with a message saying why, and SVG images and web pages (HTML), which a browser could run script in, are
/// refused by name whatever they're called.
/// <list type="bullet">
/// <item>PDF, PNG and JPEG by their signatures; HEIC and HEIF by their <c>ftyp</c> box's brands. A PDF that names
/// active content (<see cref="PdfActiveContent"/>: script, launching programs, embedded files, rich media) is refused,
/// by a search of its bytes.</item>
/// <item>Word documents: a ZIP whose parts are a Word document's, read from its central directory without unpacking
/// it. Its entry count is read first, from the end of the file, and over <see cref="MaxZipEntries"/> it's refused
/// before the archive is opened. Only <c>[Content_Types].xml</c> and the <c>.rels</c> parts are read, each up to
/// <see cref="MaxContentTypesBytes"/>. Macros, links to anything outside the document (a remote template, an external
/// object), embedded objects and ActiveX controls are refused.</item>
/// <item>Text: anything else that's valid UTF-8 with no control characters but tabs, line breaks and form feeds,
/// and doesn't start with markup (<c>&lt;</c>).</item>
/// </list>
/// These checks catch the common ways an allowed type carries active content, not all of them: a PDF's names can be
/// hidden in compressed object streams or escaped (<c>/J#61vaScript</c>), for example. The rest is left to the
/// malware scanner (<see cref="IMalwareScanner"/>), and to the files only ever being downloaded.
/// </summary>
public static class FileTypeSniffer
{
    /// <summary>How much of the start of a file the signatures and markup checks read.</summary>
    public const int HeaderBytes = 4096;

    /// <summary>The most of a Word document's <c>[Content_Types].xml</c>, or of one <c>.rels</c> part, read (each is usually 1 or 2 KB).</summary>
    public const int MaxContentTypesBytes = 64 * 1024;

    /// <summary>
    /// The most entries a Word document's ZIP may list. Real ones have tens; the table of contents is read into memory
    /// when the archive is opened, so a hostile one listing hundreds of thousands is refused before that.
    /// </summary>
    public const int MaxZipEntries = 5000;

    /// <summary>Names in a PDF for content that runs or launches something, or carries other files.</summary>
    public static readonly IReadOnlyList<string> PdfActiveContent = ["/JavaScript", "/JS", "/Launch", "/EmbeddedFile", "/RichMedia"];

    private static readonly byte[] PdfSignature = "%PDF-"u8.ToArray();
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] ZipSignature = [0x50, 0x4B, 0x03, 0x04];
    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];

    private static readonly HashSet<string> HeicBrands = ["heic", "heix", "hevc", "hevx", "heim", "heis", "hevm", "hevs"];
    private static readonly HashSet<string> HeifBrands = ["mif1", "msf1"];
    private static readonly HashSet<string> AvifBrands = ["avif", "avis"];

    private const string WordMainContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml";

    private static readonly Regex ExternalTarget = new(@"TargetMode\s*=\s*[""']External[""']", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

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
            return IdentifyPdf(content);
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

    /// <summary>
    /// A PDF, unless it names active content anywhere in its bytes (<see cref="PdfActiveContent"/>). A plain search,
    /// so a document that only mentions one of the names in its text is refused too: rare, and safe.
    /// </summary>
    private static AttachmentFileType IdentifyPdf(Stream content)
    {
        var markers = PdfActiveContent.Select(m => Encoding.ASCII.GetBytes(m)).ToList();
        var overlap = markers.Max(m => m.Length) - 1;
        var buffer = new byte[64 * 1024 + overlap];
        var kept = 0;

        content.Position = 0;
        int read;
        while ((read = content.Read(buffer, kept, buffer.Length - kept)) > 0)
        {
            var window = buffer.AsSpan(0, kept + read);
            foreach (var marker in markers)
            {
                if (window.IndexOf(marker) >= 0)
                    throw new UnsupportedFileTypeException(
                        $"PDFs with scripts, actions that open programs, or embedded files can't be attached. Print it to a new PDF, or attach {AttachmentFileTypes.AllowedText}.");
            }

            // Keep the end of this block, so a name split across two blocks is still found.
            kept = Math.Min(overlap, window.Length);
            window[^kept..].CopyTo(buffer);
        }

        return AttachmentFileTypes.Pdf;
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
        if (ZipEntryCount(content) is not { } entries)
            throw new UnsupportedFileTypeException("The file looks like a Word document or ZIP file, but it's damaged.");
        if (entries > MaxZipEntries)
            throw new UnsupportedFileTypeException(NotAllowed($"ZIP files with over {MaxZipEntries:N0} parts"));

        content.Position = 0;
        try
        {
            using var zip = new ZipArchive(content, ZipArchiveMode.Read, leaveOpen: true);

            if (zip.GetEntry("word/vbaProject.bin") is not null)
                throw new UnsupportedFileTypeException(NotAllowed("Word documents with macros"));

            if (zip.Entries.Any(e => e.FullName.StartsWith("word/embeddings/", StringComparison.OrdinalIgnoreCase)
                                     || e.FullName.StartsWith("word/activeX/", StringComparison.OrdinalIgnoreCase)))
                throw new UnsupportedFileTypeException(NotAllowed("Word documents with embedded objects or controls"));

            foreach (var rels in zip.Entries.Where(e => e.FullName.EndsWith(".rels", StringComparison.OrdinalIgnoreCase)))
            {
                if (rels.Length > MaxContentTypesBytes || ExternalTarget.IsMatch(ReadText(rels)))
                    throw new UnsupportedFileTypeException(
                        NotAllowed("Word documents that link to other files (a template or an object elsewhere)"));
            }

            var contentTypes = zip.GetEntry("[Content_Types].xml");
            if (contentTypes is null || zip.GetEntry("word/document.xml") is null || contentTypes.Length > MaxContentTypesBytes)
                throw new UnsupportedFileTypeException(NotAllowed("ZIP files and other Office documents"));

            var declared = ReadText(contentTypes);

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

    /// <summary>A small part of a ZIP as text, at most <see cref="MaxContentTypesBytes"/> characters of it.</summary>
    private static string ReadText(ZipArchiveEntry entry)
    {
        using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
        var buffer = new char[MaxContentTypesBytes];
        var read = reader.ReadBlock(buffer, 0, buffer.Length);
        return new string(buffer, 0, read);
    }

    /// <summary>
    /// How many entries a ZIP's end-of-central-directory record says it has (its ZIP64 record's count when the
    /// ordinary one is full), read from the end of the file without opening the archive; null if there's no such
    /// record.
    /// </summary>
    public static long? ZipEntryCount(Stream content)
    {
        const int eocdSize = 22;
        const int maxComment = ushort.MaxValue;
        var tailLength = (int)Math.Min(content.Length, eocdSize + maxComment);
        var tail = new byte[tailLength];
        content.Position = content.Length - tailLength;
        content.ReadExactly(tail);

        for (var i = tailLength - eocdSize; i >= 0; i--)
        {
            if (BinaryPrimitives.ReadUInt32LittleEndian(tail.AsSpan(i)) != 0x06054B50)
                continue;

            var total = BinaryPrimitives.ReadUInt16LittleEndian(tail.AsSpan(i + 10));
            if (total != ushort.MaxValue)
                return total;

            // ZIP64: the locator, just before the record, says where the ZIP64 record is; its count is at 32.
            var locatorAt = content.Length - tailLength + i - 20;
            if (locatorAt < 0)
                return null;
            var locator = new byte[20];
            content.Position = locatorAt;
            content.ReadExactly(locator);
            if (BinaryPrimitives.ReadUInt32LittleEndian(locator) != 0x07064B50)
                return null;

            var recordAt = BinaryPrimitives.ReadInt64LittleEndian(locator.AsSpan(8));
            if (recordAt < 0 || recordAt > content.Length - 56)
                return null;
            var record = new byte[56];
            content.Position = recordAt;
            content.ReadExactly(record);
            return BinaryPrimitives.ReadUInt32LittleEndian(record) == 0x06064B50
                ? BinaryPrimitives.ReadInt64LittleEndian(record.AsSpan(32))
                : null;
        }

        return null;
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
