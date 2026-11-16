using MedApp.Application.Attachments.Common;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace MedApp.Infrastructure.Attachments;

/// <summary>
/// Thumbnails of PNG and JPEG images, with SkiaSharp (MIT licence; ImageSharp's licence has commercial terms). Each is
/// decoded, turned upright (a photo's EXIF orientation), shrunk to fit <see cref="IThumbnailGenerator.MaxSide"/> and
/// encoded again, so it carries none of the original's metadata (location, camera, comments).
/// <para>
/// Decoding is bounded, since an image's header can claim any size: a JPEG is decoded already scaled down (1/2, 1/4 or
/// 1/8), and anything that would still be over <see cref="MaxDecodedPixels"/> gets no thumbnail. So does anything it
/// can't read; that's never an error, as the file itself is still attached.
/// </para>
/// </summary>
public sealed class SkiaThumbnailGenerator(ILogger<SkiaThumbnailGenerator> logger) : IThumbnailGenerator
{
    /// <summary>The most pixels decoded for one thumbnail: 24 million, about 96 MB of memory while it's made.</summary>
    public const long MaxDecodedPixels = 24_000_000;

    private const int JpegQuality = 80;

    public Task<byte[]?> CreateAsync(Stream image, AttachmentFileType type, CancellationToken ct)
    {
        if (!type.CanHaveThumbnail)
            return Task.FromResult<byte[]?>(null);

        try
        {
            ct.ThrowIfCancellationRequested();
            image.Position = 0;
            return Task.FromResult(Create(image, type));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception e)
        {
            // A damaged or hostile image: no thumbnail, and nothing worse.
            logger.LogWarning(e, "Couldn't make a thumbnail of a {ContentType} image.", type.ContentType);
            return Task.FromResult<byte[]?>(null);
        }
    }

    private static byte[]? Create(Stream image, AttachmentFileType type)
    {
        using var stream = new SKManagedStream(image, disposeManagedStream: false);
        using var codec = SKCodec.Create(stream);
        if (codec is null || codec.Info.Width <= 0 || codec.Info.Height <= 0)
            return null;

        var longest = Math.Max(codec.Info.Width, codec.Info.Height);
        var decodeSize = codec.GetScaledDimensions(Math.Min(1f, (float)IThumbnailGenerator.MaxSide / longest));
        if (decodeSize.Width <= 0 || decodeSize.Height <= 0 || (long)decodeSize.Width * decodeSize.Height > MaxDecodedPixels)
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
