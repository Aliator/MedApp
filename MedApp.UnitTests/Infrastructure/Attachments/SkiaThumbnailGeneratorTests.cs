using FluentAssertions;
using MedApp.Application.Attachments.Common;
using MedApp.Infrastructure.Attachments;
using Microsoft.Extensions.Logging.Abstractions;
using SkiaSharp;
using static MedApp.UnitTests.Common.Constants.AttachmentsTestConstants;

namespace MedApp.UnitTests.Infrastructure.Attachments;

[TestFixture]
public sealed class SkiaThumbnailGeneratorTests
{
    private readonly SkiaThumbnailGenerator _generator = new(NullLogger<SkiaThumbnailGenerator>.Instance);

    private async Task<SKBitmap?> Thumbnail(byte[] image, AttachmentFileType type)
    {
        await using var stream = new MemoryStream(image);
        var thumbnail = await _generator.CreateAsync(stream, type, CancellationToken.None);
        return thumbnail is null ? null : SKBitmap.Decode(thumbnail);
    }

    [Test]
    public async Task CreateAsync_LargePng_ShrinksItToFitAndKeepsItAPng()
    {
        var image = Image(1200, 600, SKEncodedImageFormat.Png);
        await using var stream = new MemoryStream(image);

        var thumbnail = await _generator.CreateAsync(stream, AttachmentFileTypes.Png, CancellationToken.None);

        thumbnail.Should().NotBeNull();
        thumbnail!.Take(8).Should().Equal(PngHeader.Take(8));
        using var decoded = SKBitmap.Decode(thumbnail);
        (decoded.Width, decoded.Height).Should().Be((IThumbnailGenerator.MaxSide, IThumbnailGenerator.MaxSide / 2));
    }

    [Test]
    public async Task CreateAsync_Jpeg_IsAJpegThatFits()
    {
        var image = Image(640, 960, SKEncodedImageFormat.Jpeg);
        await using var stream = new MemoryStream(image);

        var thumbnail = await _generator.CreateAsync(stream, AttachmentFileTypes.Jpeg, CancellationToken.None);

        thumbnail!.Take(3).Should().Equal(JpegHeader.Take(3));
        using var decoded = SKBitmap.Decode(thumbnail);
        Math.Max(decoded.Width, decoded.Height).Should().BeLessThanOrEqualTo(IThumbnailGenerator.MaxSide);
    }

    [Test]
    public async Task CreateAsync_SmallImage_IsNeverEnlarged()
    {
        using var decoded = await Thumbnail(Image(40, 30, SKEncodedImageFormat.Png), AttachmentFileTypes.Png);

        (decoded!.Width, decoded.Height).Should().Be((40, 30));
    }

    [Test]
    public async Task CreateAsync_StreamNotAtTheStart_ReadsFromTheStart()
    {
        await using var stream = new MemoryStream(Image(50, 50, SKEncodedImageFormat.Png));
        stream.Position = stream.Length;

        (await _generator.CreateAsync(stream, AttachmentFileTypes.Png, CancellationToken.None)).Should().NotBeNull();
    }

    [Test]
    public async Task CreateAsync_NotAnImageAtAll_ReturnsNull()
    {
        (await Thumbnail(PngHeader, AttachmentFileTypes.Png)).Should().BeNull();
    }

    [Test]
    public async Task CreateAsync_TypeWithoutThumbnails_ReturnsNull()
    {
        (await Thumbnail(Pdf, AttachmentFileTypes.Pdf)).Should().BeNull();
    }

    [Test]
    public async Task CreateAsync_HeaderClaimingAHugeImage_ReturnsNullWithoutDecodingIt()
    {
        // A real PNG whose header says 20,000 x 20,000 (400 million pixels): far over the limit, so never decoded.
        var png = Image(10, 10, SKEncodedImageFormat.Png);
        WriteBigEndian(png, 16, 20_000);
        WriteBigEndian(png, 20, 20_000);
        FixIhdrCrc(png);

        (await Thumbnail(png, AttachmentFileTypes.Png)).Should().BeNull();
    }

    [Test]
    public async Task CreateAsync_Cancelled_Throws()
    {
        await using var stream = new MemoryStream(Image(10, 10, SKEncodedImageFormat.Png));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = () => _generator.CreateAsync(stream, AttachmentFileTypes.Png, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Test]
    public async Task CreateAsync_UnreadableStream_ReturnsNull()
    {
        var act = await _generator.CreateAsync(new BrokenStream(), AttachmentFileTypes.Png, CancellationToken.None);

        act.Should().BeNull();
    }

    [TestCase(1, 200, 100)]
    [TestCase(2, 200, 100)]
    [TestCase(3, 200, 100)]
    [TestCase(4, 200, 100)]
    [TestCase(5, 100, 200)]
    [TestCase(6, 100, 200)]
    [TestCase(7, 100, 200)]
    [TestCase(8, 100, 200)]
    public async Task CreateAsync_JpegWithAnExifOrientation_IsTurnedUpright(int orientation, int width, int height)
    {
        using var decoded = await Thumbnail(WithExifOrientation(Image(200, 100, SKEncodedImageFormat.Jpeg), orientation), AttachmentFileTypes.Jpeg);

        (decoded!.Width, decoded.Height).Should().Be((width, height));
    }

    [Test]
    public async Task CreateAsync_JpegTurnedRight_PutsTheTopRowOnTheRight()
    {
        // The stored image's top half is red; turned 90 degrees clockwise (EXIF 6), red should be on the right.
        using var bitmap = new SKBitmap(200, 100);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Blue);
            canvas.DrawRect(0, 0, 200, 50, new SKPaint { Color = SKColors.Red });
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 95);

        using var decoded = await Thumbnail(WithExifOrientation(data.ToArray(), 6), AttachmentFileTypes.Jpeg);

        decoded!.GetPixel(decoded.Width - 5, decoded.Height / 2).Red.Should().BeGreaterThan(200);
        decoded.GetPixel(5, decoded.Height / 2).Blue.Should().BeGreaterThan(200);
    }

    [TestCase(1000, 500, 320, 320, 160)]
    [TestCase(500, 1000, 320, 160, 320)]
    [TestCase(100, 50, 320, 100, 50)]
    [TestCase(10000, 1, 320, 320, 1)]
    public void Fit_KeepsTheShapeWithinTheSquare(int width, int height, int maxSide, int expectedWidth, int expectedHeight)
    {
        SkiaThumbnailGenerator.Fit(width, height, maxSide).Should().Be((expectedWidth, expectedHeight));
    }

    /// <summary>The JPEG with an EXIF segment (APP1) saying how it's oriented (1 to 8), straight after its start marker.</summary>
    private static byte[] WithExifOrientation(byte[] jpeg, int orientation)
    {
        byte[] tiff =
        [
            (byte)'M', (byte)'M', 0, 42, 0, 0, 0, 8, // big-endian TIFF header, first IFD at 8
            0, 1, // one entry
            0x01, 0x12, 0, 3, 0, 0, 0, 1, 0, (byte)orientation, 0, 0, // Orientation, SHORT, 1 value
            0, 0, 0, 0, // no next IFD
        ];
        var payload = "Exif\0\0"u8.ToArray().Concat(tiff).ToArray();
        var length = payload.Length + 2;
        byte[] app1 = [0xFF, 0xE1, (byte)(length >> 8), (byte)length, .. payload];

        return [.. jpeg.Take(2), .. app1, .. jpeg.Skip(2)];
    }

    private static void WriteBigEndian(byte[] bytes, int offset, int value)
    {
        bytes[offset] = (byte)(value >> 24);
        bytes[offset + 1] = (byte)(value >> 16);
        bytes[offset + 2] = (byte)(value >> 8);
        bytes[offset + 3] = (byte)value;
    }

    /// <summary>The IHDR chunk's CRC (over its type and 13 bytes of data), so the decoder reads the edited header.</summary>
    private static void FixIhdrCrc(byte[] png)
    {
        var crc = System.IO.Hashing.Crc32.HashToUInt32(png.AsSpan(12, 17));
        WriteBigEndian(png, 29, unchecked((int)crc));
    }

    private sealed class BrokenStream : MemoryStream
    {
        public override int Read(byte[] buffer, int offset, int count) => throw new IOException("Synthetic read failure.");
        public override int Read(Span<byte> buffer) => throw new IOException("Synthetic read failure.");
    }
}
