using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using Azure;
using FluentAssertions;
using MedApp.Application.Attachments.Common;
using MedApp.Infrastructure.Attachments;
using Microsoft.Extensions.Logging.Abstractions;
using static MedApp.UnitTests.Common.Constants.AttachmentsTestConstants;

namespace MedApp.UnitTests.Infrastructure.Attachments;

/// <summary>
/// Files made to hurt the API or a workstation (PR #85 review, findings 1, 8 and 9): images whose header claims a size
/// that would exhaust memory, ZIPs listing too many parts, and Word and PDF files with active content.
/// </summary>
[TestFixture]
public sealed class HostileFileTests
{
    private readonly SkiaThumbnailGenerator _thumbnails = new(NullLogger<SkiaThumbnailGenerator>.Instance);

    /// <summary>
    /// A grey progressive JPEG (SOF2) of the given size whose every 8 x 8 block is "no change", so it compresses to one
    /// bit a block: 24,000 x 24,000 is about 1.1 MB, but decoding it needs its full-size coefficients, over 1 GB.
    /// </summary>
    private static byte[] ProgressiveJpegBomb(int width, int height)
    {
        var bytes = new List<byte>();
        void W(params int[] values) => bytes.AddRange(values.Select(v => (byte)v));

        W(0xFF, 0xD8);                                                                       // SOI
        W(0xFF, 0xDB, 0x00, 0x43, 0x00); bytes.AddRange(Enumerable.Repeat((byte)1, 64));     // DQT
        W(0xFF, 0xC2, 0x00, 0x0B, 0x08, height >> 8, height & 0xFF, width >> 8, width & 0xFF, 0x01, 0x01, 0x11, 0x00); // SOF2
        W(0xFF, 0xC4, 0x00, 0x14, 0x00, 0x01); bytes.AddRange(new byte[15]); W(0x00);        // DHT: one 1-bit code
        W(0xFF, 0xDA, 0x00, 0x08, 0x01, 0x01, 0x00, 0x00, 0x00, 0x00);                        // SOS: DC first scan
        var blocks = (long)((width + 7) / 8) * ((height + 7) / 8);
        bytes.AddRange(new byte[(int)(blocks / 8)]);
        W(0xFF, 0xD9);                                                                       // EOI
        return bytes.ToArray();
    }

    [Test]
    public async Task Thumbnail_ProgressiveJpegClaimingAHugeSize_IsSkippedQuicklyWithoutDecoding()
    {
        var bomb = ProgressiveJpegBomb(24_000, 24_000);
        bomb.Length.Should().BeLessThan(1_200_000, "the bomb is a small file");
        var before = GC.GetTotalAllocatedBytes(precise: true);
        var timer = Stopwatch.StartNew();

        await using var stream = new MemoryStream(bomb);
        var thumbnail = await _thumbnails.CreateAsync(stream, AttachmentFileTypes.Jpeg, CancellationToken.None);

        thumbnail.Should().BeNull();
        timer.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5));
        (GC.GetTotalAllocatedBytes(precise: true) - before).Should().BeLessThan(64L * 1024 * 1024, "nothing is decoded");
    }

    [Test]
    public async Task Thumbnail_SmallProgressiveJpeg_StillGetsOne()
    {
        await using var stream = new MemoryStream(ProgressiveJpegBomb(800, 600));

        (await _thumbnails.CreateAsync(stream, AttachmentFileTypes.Jpeg, CancellationToken.None)).Should().NotBeNull();
    }

    [Test]
    public async Task Thumbnail_OneSideOverTheLimit_IsSkippedThoughItHasFewPixels()
    {
        await using var stream = new MemoryStream(ProgressiveJpegBomb(SkiaThumbnailGenerator.MaxSourceSide + 8, 16));

        (await _thumbnails.CreateAsync(stream, AttachmentFileTypes.Jpeg, CancellationToken.None)).Should().BeNull();
    }

    [TestCase(4000, 6000, true)]
    [TestCase(6000, 4000, true)]
    [TestCase(10_000, 2_400, true)]
    [TestCase(10_001, 100, false)]
    [TestCase(100, 10_001, false)]
    [TestCase(5_000, 5_000, false)]
    [TestCase(0, 10, false)]
    public void IsSmallEnough_TheSourcesOwnSize(int width, int height, bool expected)
    {
        SkiaThumbnailGenerator.IsSmallEnough(width, height).Should().Be(expected);
    }

    [Test]
    public async Task Limiter_LetsOnlyTwoRunAtOnce()
    {
        var release = new TaskCompletionSource();
        var running = 0;
        var most = 0;
        var gate = new object();

        async Task<int> Work()
        {
            lock (gate)
                most = Math.Max(most, ++running);
            await release.Task;
            lock (gate)
                running--;
            return 0;
        }

        var tasks = Enumerable.Range(0, 5).Select(_ => FileWorkLimiter.RunAsync(Work, CancellationToken.None)).ToList();
        await Task.Delay(100);
        most.Should().Be(FileWorkLimiter.MaxConcurrent);
        release.SetResult();
        await Task.WhenAll(tasks);
        FileWorkLimiter.Available.Should().Be(FileWorkLimiter.MaxConcurrent);
    }

    [Test]
    public async Task Limiter_ReleasesItsPlaceWhenWorkThrows()
    {
        var act = () => FileWorkLimiter.RunAsync(new Func<int>(() => throw new InvalidOperationException("Synthetic failure.")), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        FileWorkLimiter.Available.Should().Be(FileWorkLimiter.MaxConcurrent);
    }

    private static AttachmentFileType Identify(byte[] bytes, string name)
    {
        using var stream = new MemoryStream(bytes);
        return FileTypeSniffer.Identify(stream, name);
    }

    private static byte[] DocxWith(int extraEntries = 0, params (string Name, string Content)[] parts)
    {
        var entries = new List<(string, string)>
        {
            ("[Content_Types].xml", $"<Types><Override PartName=\"/word/document.xml\" ContentType=\"{WordMain}\"/></Types>"),
            ("word/document.xml", "<w:document/>"),
        };
        entries.AddRange(parts);
        entries.AddRange(Enumerable.Range(0, extraEntries).Select(i => ($"x/{i}", "")));
        return Zip(entries.ToArray());
    }

    [Test]
    public void Docx_AtTheEntryLimit_IsAccepted()
    {
        Identify(DocxWith(FileTypeSniffer.MaxZipEntries - 2), "letter.docx").Should().Be(AttachmentFileTypes.Docx);
    }

    [Test]
    public void Docx_OverTheEntryLimit_IsRefusedBeforeItIsOpened()
    {
        var act = () => Identify(DocxWith(FileTypeSniffer.MaxZipEntries - 1), "letter.docx");

        act.Should().Throw<UnsupportedFileTypeException>().WithMessage("ZIP files with over 5,000 parts can't be attached*");
    }

    [Test]
    public void ZipEntryCount_Zip64_ReadsTheZip64Count()
    {
        // Over 65,535 entries, the ordinary record is full and the ZIP64 record has the count.
        var zip = DocxWith(70_000);
        using var stream = new MemoryStream(zip);

        FileTypeSniffer.ZipEntryCount(stream).Should().Be(70_002);
        FluentActions.Invoking(() => Identify(zip, "letter.docx")).Should().Throw<UnsupportedFileTypeException>();
    }

    [Test]
    public void ZipEntryCount_NoEndRecord_IsNull()
    {
        using var stream = new MemoryStream(ZipSignatureThenNothing());

        FileTypeSniffer.ZipEntryCount(stream).Should().BeNull();
        FluentActions.Invoking(() => Identify(ZipSignatureThenNothing(), "letter.docx"))
            .Should().Throw<UnsupportedFileTypeException>().WithMessage("*damaged*");
    }

    private static byte[] ZipSignatureThenNothing() => [0x50, 0x4B, 0x03, 0x04, .. new byte[200]];

    [TestCase("word/_rels/settings.xml.rels", "<Relationships><Relationship Id=\"r1\" Type=\"attachedTemplate\" Target=\"https://example.invalid/t.dotm\" TargetMode=\"External\"/></Relationships>")]
    [TestCase("word/_rels/document.xml.rels", "<Relationships><Relationship Id='r2' Type='oleObject' Target='file://server/share/x' targetmode = 'external'/></Relationships>")]
    public void Docx_LinkingOutside_IsRefused(string part, string rels)
    {
        var act = () => Identify(DocxWith(0, (part, rels)), "letter.docx");

        act.Should().Throw<UnsupportedFileTypeException>().WithMessage("Word documents that link to other files*");
    }

    [Test]
    public void Docx_WithInternalLinksOnly_IsAccepted()
    {
        var rels = "<Relationships><Relationship Id=\"r1\" Type=\"styles\" Target=\"styles.xml\"/></Relationships>";

        Identify(DocxWith(0, ("word/_rels/document.xml.rels", rels)), "letter.docx").Should().Be(AttachmentFileTypes.Docx);
    }

    [TestCase("word/embeddings/oleObject1.bin")]
    [TestCase("word/activeX/activeX1.xml")]
    public void Docx_WithEmbeddedObjectsOrControls_IsRefused(string part)
    {
        var act = () => Identify(DocxWith(0, (part, "x")), "letter.docx");

        act.Should().Throw<UnsupportedFileTypeException>().WithMessage("Word documents with embedded objects or controls*");
    }

    [Test]
    public void Docx_HugeRelationshipsPart_IsRefusedWithoutReadingIt()
    {
        var act = () => Identify(DocxWith(0, ("_rels/.rels", new string(' ', FileTypeSniffer.MaxContentTypesBytes + 1))), "letter.docx");

        act.Should().Throw<UnsupportedFileTypeException>();
    }

    [TestCase("/JavaScript")]
    [TestCase("/JS")]
    [TestCase("/Launch")]
    [TestCase("/EmbeddedFile")]
    [TestCase("/RichMedia")]
    public void Pdf_WithActiveContent_IsRefused(string marker)
    {
        var pdf = Encoding.ASCII.GetBytes($"%PDF-1.7\n1 0 obj\n<< /Type /Catalog /OpenAction << /S {marker} >> >>\nendobj\n%%EOF\n");

        var act = () => Identify(pdf, "letter.pdf");

        act.Should().Throw<UnsupportedFileTypeException>().WithMessage("PDFs with scripts, actions that open programs, or embedded files can't be attached*");
    }

    [Test]
    public void Pdf_MarkerAcrossTwoBlocks_IsStillFound()
    {
        var start = Encoding.ASCII.GetBytes("%PDF-1.7\n");
        var filler = Enumerable.Repeat((byte)' ', 64 * 1024 - start.Length - 4).ToArray();
        var pdf = start.Concat(filler).Concat(Encoding.ASCII.GetBytes("/JavaScript (x)\n%%EOF")).ToArray();

        FluentActions.Invoking(() => Identify(pdf, "letter.pdf")).Should().Throw<UnsupportedFileTypeException>();
    }

    [Test]
    public void Pdf_WithoutActiveContent_IsAccepted()
    {
        Identify(Pdf, "letter.pdf").Should().Be(AttachmentFileTypes.Pdf);
    }

    [Test]
    public async Task Storage_Failures_BecomeStorageUnavailable()
    {
        var failed = () => BlobFileStore.Guard<int>(() => throw new RequestFailedException(500, "Synthetic"), CancellationToken.None);
        var timedOut = () => BlobFileStore.Guard<int>(() => throw new TaskCanceledException("Network timeout"), CancellationToken.None);
        var broken = () => BlobFileStore.Guard<int>(() => throw new IOException("Connection reset"), CancellationToken.None);

        (await failed.Should().ThrowAsync<StorageUnavailableException>()).WithInnerException<RequestFailedException>();
        await timedOut.Should().ThrowAsync<StorageUnavailableException>();
        await broken.Should().ThrowAsync<StorageUnavailableException>();
    }

    [Test]
    public async Task Storage_CancelledByTheCaller_StaysCancelled()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = () => BlobFileStore.Guard<int>(() => throw new OperationCanceledException(cts.Token), cts.Token);

        (await act.Should().ThrowAsync<OperationCanceledException>()).Which.InnerException.Should().BeNull();
    }

    [Test]
    public void ClientOptions_RetryTwiceWithAThirtySecondTimeout()
    {
        var options = AttachmentStorageOptions.ClientOptions();

        options.Retry.MaxRetries.Should().Be(2);
        options.Retry.NetworkTimeout.Should().Be(TimeSpan.FromSeconds(30));
    }
}
