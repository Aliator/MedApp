namespace MedApp.Application.Attachments.Common;

/// <summary>
/// Makes thumbnails of uploaded images (<see cref="AttachmentFileType.CanHaveThumbnail"/>): small and re-encoded, so
/// they carry none of the original's metadata (a photo's location, its camera), and safe to show on a page.
/// </summary>
public interface IThumbnailGenerator
{
    /// <summary>The longest side of a thumbnail, in pixels.</summary>
    public const int MaxSide = 320;

    /// <summary>
    /// A thumbnail of the image in <paramref name="image"/> (seekable, read from the start), encoded as
    /// <see cref="ContentTypeOf"/> says, or null when it can't make one: an image it can't read, or one too large to
    /// decode safely. Never throws for a bad image.
    /// </summary>
    Task<byte[]?> CreateAsync(Stream image, AttachmentFileType type, CancellationToken ct);

    /// <summary>What a thumbnail of this type of image is encoded as: PNG for a PNG (it may be transparent), else JPEG.</summary>
    public static string ContentTypeOf(AttachmentFileType type) =>
        type == AttachmentFileTypes.Png ? "image/png" : "image/jpeg";
}
