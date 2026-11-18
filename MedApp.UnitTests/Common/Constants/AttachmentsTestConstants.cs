using System.IO.Compression;
using System.Text;
using MedApp.Application.Attachments.Common;
using MedApp.Domain.Attachments;
using MedApp.Domain.Pathways.PatientPathways;
using SkiaSharp;

namespace MedApp.UnitTests.Common.Constants;

/// <summary>Synthetic files of every kind the sniffer knows, and attachments to test with.</summary>
public static class AttachmentsTestConstants
{
    public static readonly Guid PatientId = Guid.Parse("0000000f-0000-0000-0000-000000000001");
    public static readonly Guid AttachmentId = Guid.Parse("0000000f-0000-0000-0000-000000000002");
    public const string FileName = "synthetic-scan.pdf";
    public const string Sha256OfNothing = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

    public static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.7\n1 0 obj\n<< /Type /Catalog >>\nendobj\ntrailer\n%%EOF\n");
    public static readonly byte[] PngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, 0x49, 0x48, 0x44, 0x52];
    public static readonly byte[] JpegHeader = [0xFF, 0xD8, 0xFF, 0xE0, 0, 16, 0x4A, 0x46, 0x49, 0x46, 0];
    public static readonly byte[] Text = Encoding.UTF8.GetBytes("Synthetic results letter.\r\n\tAll values normal.\f\n");
    public static readonly byte[] Gif = Encoding.ASCII.GetBytes("GIF89a\u0001\u0000\u0001\u0000\u0000\u0000\u0000;");
    public static readonly byte[] Executable = [(byte)'M', (byte)'Z', 0x90, 0, 3, 0, 0, 0, 4, 0, 0, 0, 0xFF, 0xFF];

    /// <summary>An ISO media file's ftyp box with these brands: the first is the major brand.</summary>
    public static byte[] IsoMedia(params string[] brands)
    {
        var compatible = brands.Skip(1).ToArray();
        var size = 16 + 4 * compatible.Length;
        var bytes = new List<byte> { 0, 0, 0, (byte)size };
        bytes.AddRange("ftyp"u8.ToArray());
        bytes.AddRange(Encoding.ASCII.GetBytes(brands[0]));
        bytes.AddRange([0, 0, 0, 0]);
        foreach (var brand in compatible)
            bytes.AddRange(Encoding.ASCII.GetBytes(brand));
        bytes.AddRange(new byte[32]);
        return bytes.ToArray();
    }

    public const string WordMain = "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml";

    /// <summary>A ZIP with these entries (name and text), like an Office document.</summary>
    public static byte[] Zip(params (string Name, string Content)[] entries)
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                using var writer = new StreamWriter(zip.CreateEntry(name).Open(), Encoding.UTF8);
                writer.Write(content);
            }
        }

        return buffer.ToArray();
    }

    /// <summary>The smallest Word document the sniffer accepts.</summary>
    public static byte[] Docx(string mainContentType = WordMain, bool withMacros = false)
    {
        var entries = new List<(string, string)>
        {
            ("[Content_Types].xml", $"<?xml version=\"1.0\"?><Types><Override PartName=\"/word/document.xml\" ContentType=\"{mainContentType}\"/></Types>"),
            ("word/document.xml", "<w:document/>"),
        };
        if (withMacros)
            entries.Add(("word/vbaProject.bin", "macro"));
        return Zip(entries.ToArray());
    }

    /// <summary>A real image, solid colour, encoded as <paramref name="format"/>.</summary>
    public static byte[] Image(int width, int height, SKEncodedImageFormat format)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(SKColors.SteelBlue);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 90);
        return data.ToArray();
    }

    public static MemoryStream Stream(byte[] bytes) => new(bytes, writable: false);

    public static Attachment CreateAttachment(
        Guid? uploadedBy = null,
        Guid? pathwayId = null,
        Guid? stageId = null,
        bool deleted = false,
        bool hasThumbnail = false,
        string contentType = "application/pdf",
        string fileName = FileName) => new()
    {
        Id = Guid.NewGuid(),
        PatientId = PatientId,
        PathwayId = pathwayId,
        StageId = stageId,
        FileName = fileName,
        ContentType = contentType,
        SizeBytes = 1234,
        Sha256 = new string('a', 64),
        ScanStatus = AttachmentScanStatus.NotScanned,
        HasThumbnail = hasThumbnail,
        UploadedByUserId = uploadedBy ?? PathwaysTestConstants.Staff.UserId,
        UploadedByUsername = PathwaysTestConstants.Staff.Username,
        UploadedAtUtc = PathwaysTestConstants.Now.AddHours(-1),
        DeletedAtUtc = deleted ? PathwaysTestConstants.Now.AddMinutes(-5) : null,
        DeletedByUserId = deleted ? PathwaysTestConstants.Admin.UserId : null,
        DeletedByUsername = deleted ? PathwaysTestConstants.Admin.Username : null,
    };

    /// <summary>An attachment on the pathway's stage at <paramref name="order"/> (1-based), or on the pathway itself.</summary>
    public static Attachment AttachmentOn(PatientPathway pathway, int? stageOrder = null, Guid? uploadedBy = null) =>
        CreateAttachment(uploadedBy, pathway.Id, stageOrder is { } order ? pathway.StageAt(order).Id : null);

    /// <summary>The file types every test of "all of them" goes through.</summary>
    public static IEnumerable<(byte[] Bytes, string Name, AttachmentFileType Type)> EveryAllowedFile() =>
    [
        (Pdf, "letter.pdf", AttachmentFileTypes.Pdf),
        (PngHeader, "photo.png", AttachmentFileTypes.Png),
        (JpegHeader, "photo.jpg", AttachmentFileTypes.Jpeg),
        (JpegHeader, "photo.JPEG", AttachmentFileTypes.Jpeg),
        (IsoMedia("heic", "mif1", "heic"), "photo.heic", AttachmentFileTypes.Heic),
        (IsoMedia("mif1", "mif1"), "photo.heif", AttachmentFileTypes.Heif),
        (Docx(), "letter.docx", AttachmentFileTypes.Docx),
        (Text, "notes.txt", AttachmentFileTypes.Text),
    ];
}
