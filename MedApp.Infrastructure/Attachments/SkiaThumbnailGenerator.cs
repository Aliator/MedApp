using MedApp.Application.Attachments.Common;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace MedApp.Infrastructure.Attachments;

/// <summary>
/// Thumbnails of PNG and JPEG images, with SkiaSharp (MIT licence; ImageSharp's licence has commercial terms). Each is
/// decoded, turned upright (a photo's EXIF orientation), shrunk to fit <see cref="IThumbnailGenerator.MaxSide"/> and
/// encoded again, so it carries none of the original's metadata (location, camera, comments).
/// <para>
/// An image's header can claim any size, and a small file can claim a huge one. Decoding scaled down doesn't bound the
/// memory on its own: a progressive JPEG holds its coefficients at full size whatever the scale (about 2 bytes a
/// pixel), and a PNG decodes at full size. So the header is read first, and an image whose <b>own</b> size is over
/// <see cref="MaxSourcePixels"/>, or either side over <see cref="MaxSourceSide"/>, gets no thumbnail; neither does
/// anything it can't read. That's never an error: the file itself is still attached, without a preview. At most
/// <see cref="FileWorkLimiter.MaxConcurrent"/> thumbnails are made at once in each copy of the API.
/// </para>
/// <para>
/// <b>Skia never reads a managed stream.</b> The file's bytes are read into memory here, in managed code, and Skia
/// decodes from those (<see cref="SKData"/>). A managed stream handed to Skia is read from inside its native code, and
/// an exception thrown there (a read failing) can't be caught: it ends the process. Reading first is bounded by the
/// upload limit (<see cref="AttachmentLimits.MaxFileBytes"/>, and refused above it); the pixels are bounded by the header
/// check, which needs no decoding.
/// </para>
/// </summary>
public sealed class SkiaThumbnailGenerator(ILogger<SkiaThumbnailGenerator> logger) : IThumbnailGenerator
{
    /// <summary>
    /// The largest source image a thumbnail is made of: 24 million pixels (a 24-megapixel photo), at most about 96 MB
    /// decoded (or 48 MB of a progressive JPEG's coefficients). Two at once stay well inside the API's 1 GiB.
    /// </summary>
    public const long MaxSourcePixels = 24_000_000;

    /// <summary>The longest side of a source image a thumbnail is made of.</summary>
    public const int MaxSourceSide = 10_000;

    private const int JpegQuality = 80;

    public Task<byte[]?> CreateAsync(Stream image, AttachmentFileType type, CancellationToken ct)
    {
        if (!type.CanHaveThumbnail)
            return Task.FromResult<byte[]?>(null);

        return FileWorkLimiter.RunAsync(() => CreateSafely(image, type, ct), ct);
    }

    private byte[]? CreateSafely(Stream image, AttachmentFileType type, CancellationToken ct)
    {
        try
        {
            ct.ThrowIfCancellationRequested();
            image.Position = 0;
            return Create(image, type);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception e)
        {
            // A damaged or hostile image: no thumbnail, and nothing worse.
            logger.LogWarning(e, "Couldn't make a thumbnail of a {ContentType} image.", type.ContentType);
            return null;
        }
    }

    /// <summary>Whether an image this size may be decoded for a thumbnail (its own size, from its header).</summary>
    public static bool IsSmallEnough(int width, int height) =>
        width > 0 && height > 0 && width <= MaxSourceSide && height <= MaxSourceSide
        && (long)width * height <= MaxSourcePixels;

    private static byte[]? Create(Stream image, AttachmentFileType type)
    {
        using var source = ReadAll(image);
        if (source is null)
            return null;

        using var codec = SKCodec.Create(source);
        // The source's own size, from its header, before anything is decoded (see the class comment).
        if (codec is null || !IsSmallEnough(codec.Info.Width, codec.Info.Height))
            return null;

        var longest = Math.Max(codec.Info.Width, codec.Info.Height);
        var decodeSize = codec.GetScaledDimensions(Math.Min(1f, (float)IThumbnailGenerator.MaxSide / longest));
        if (decodeSize.Width <= 0 || decodeSize.Height <= 0)
            return null;

        var decodeInfo = new SKImageInfo(decodeSize.Width, decodeSize.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var decoded = new SKBitmap(decodeInfo);
        var result = codec.GetPixels(decodeInfo, decoded.GetPixels());
        if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
            return null;

        var (width, height) = Fit(decoded.Width, decoded.Height, IThumbnailGenerator.MaxSide);
        using var resized = decoded.Resize(
            new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul),
            new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        if (resized is null)
            return null;

        using var upright = Orient(resized, codec.EncodedOrigin);
        using var encoded = SKImage.FromBitmap(upright ?? resized);
        using var data = type == AttachmentFileTypes.Png
            ? encoded.Encode(SKEncodedImageFormat.Png, 100)
            : encoded.Encode(SKEncodedImageFormat.Jpeg, JpegQuality);

        return data?.ToArray();
    }

    /// <summary>
    /// The whole image, read in managed code (so a failing read throws here, where it's caught) into a buffer Skia can
    /// decode from; null if it's larger than an upload can be.
    /// </summary>
    private static SKData? ReadAll(Stream image)
    {
        if (image.CanSeek && image.Length - image.Position > AttachmentLimits.MaxFileBytes)
            return null;

        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = image.Read(chunk, 0, chunk.Length)) > 0)
        {
            if (buffer.Length + read > AttachmentLimits.MaxFileBytes)
                return null;
            buffer.Write(chunk, 0, read);
        }

        return SKData.CreateCopy(buffer.GetBuffer().AsSpan(0, (int)buffer.Length));
    }

    /// <summary>The size within a square of <paramref name="maxSide"/>, keeping the shape; never larger than it was.</summary>
    public static (int Width, int Height) Fit(int width, int height, int maxSide)
    {
        var scale = Math.Min(1.0, (double)maxSide / Math.Max(width, height));
        return (Math.Max(1, (int)Math.Round(width * scale)), Math.Max(1, (int)Math.Round(height * scale)));
    }

    /// <summary>
    /// The image turned the way its EXIF orientation says it should be shown, or null when it's upright already.
    /// Each matrix maps a stored pixel (x, y) to where it's shown.
    /// </summary>
    private static SKBitmap? Orient(SKBitmap image, SKEncodedOrigin origin)
    {
        float w = image.Width, h = image.Height;
        SKMatrix? matrix = origin switch
        {
            SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, w, 0, 1, 0, 0, 0, 1),
            SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, w, 0, -1, h, 0, 0, 1),
            SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, h, 0, 0, 1),
            SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightTop => new SKMatrix(0, -1, h, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, h, -1, 0, w, 0, 0, 1),
            SKEncodedOrigin.LeftBottom => new SKMatrix(0, 1, 0, -1, 0, w, 0, 0, 1),
            _ => null,
        };
        if (matrix is null)
            return null;

        var sideways = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var turned = new SKBitmap(new SKImageInfo(
            sideways ? image.Height : image.Width,
            sideways ? image.Width : image.Height,
            image.ColorType,
            image.AlphaType));

        using var canvas = new SKCanvas(turned);
        canvas.SetMatrix(matrix.Value);
        canvas.DrawBitmap(image, 0, 0);
        canvas.Flush();
        return turned;
    }
}
