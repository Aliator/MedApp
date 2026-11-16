namespace MedApp.Application.Attachments.Common;

/// <summary>A kind of file that can be attached, as <see cref="FileTypeSniffer"/> recognises it from its contents.</summary>
/// <param name="Name">The type in words, e.g. "PDF".</param>
/// <param name="ContentType">The media type it's stored and served as.</param>
/// <param name="Extensions">The extensions a file of this type may have (lower case, with the dot); the first is usual.</param>
/// <param name="CanHaveThumbnail">Whether the API makes a thumbnail of it (PNG and JPEG).</param>
public sealed record AttachmentFileType(
    string Name,
    string ContentType,
    IReadOnlyList<string> Extensions,
    bool CanHaveThumbnail);

/// <summary>Every kind of file that can be attached. Anything else is refused, SVG and HTML explicitly.</summary>
public static class AttachmentFileTypes
{
    public static readonly AttachmentFileType Pdf = new("PDF", "application/pdf", [".pdf"], false);

    public static readonly AttachmentFileType Png = new("PNG image", "image/png", [".png"], true);

    public static readonly AttachmentFileType Jpeg = new("JPEG image", "image/jpeg", [".jpg", ".jpeg"], true);

    /// <summary>An iPhone photo (HEIF with HEVC). No thumbnail: the image library can't read it on Linux.</summary>
    public static readonly AttachmentFileType Heic = new("HEIC image", "image/heic", [".heic", ".heif"], false);

    /// <summary>Another HEIF image.</summary>
    public static readonly AttachmentFileType Heif = new("HEIF image", "image/heif", [".heif", ".heic"], false);

    public static readonly AttachmentFileType Docx = new(
        "Word document",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".docx"],
        false);

    public static readonly AttachmentFileType Text = new("Text", "text/plain; charset=utf-8", [".txt"], false);

    public static readonly IReadOnlyList<AttachmentFileType> All = [Pdf, Png, Jpeg, Heic, Heif, Docx, Text];

    /// <summary>Every extension a file can have, for a file picker's hint.</summary>
    public static IReadOnlyList<string> Extensions => All.SelectMany(t => t.Extensions).Distinct().ToList();

    /// <summary>The type a stored content type stands for; null for one that isn't (or is no longer) allowed.</summary>
    public static AttachmentFileType? FromContentType(string contentType) =>
        All.FirstOrDefault(t => string.Equals(t.ContentType, contentType, StringComparison.OrdinalIgnoreCase));

    /// <summary>The allowed types in words, for messages.</summary>
    public const string AllowedText = "PDF, PNG, JPEG, HEIC or HEIF images, Word documents (.docx) and plain text (.txt)";
}
