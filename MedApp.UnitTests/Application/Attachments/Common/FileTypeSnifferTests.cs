using System.Text;
using FluentAssertions;
using MedApp.Application.Attachments.Common;
using static MedApp.UnitTests.Common.Constants.AttachmentsTestConstants;

namespace MedApp.UnitTests.Application.Attachments.Common;

[TestFixture]
public sealed class FileTypeSnifferTests
{
    private static AttachmentFileType Identify(byte[] bytes, string name)
    {
        using var stream = new MemoryStream(bytes);
        return FileTypeSniffer.Identify(stream, name);
    }

    private static Func<AttachmentFileType> Identifying(byte[] bytes, string name) => () => Identify(bytes, name);

    private static IEnumerable<TestCaseData> AllowedFiles() =>
        EveryAllowedFile().Select(f => new TestCaseData(f.Bytes, f.Name, f.Type).SetName($"Identify_{f.Name}_Is{f.Type.Name.Replace(" ", "")}"));

    [TestCaseSource(nameof(AllowedFiles))]
    public void Identify_AllowedFileWithItsExtension_ReturnsItsType(byte[] bytes, string name, AttachmentFileType expected)
    {
        Identify(bytes, name).Should().Be(expected);
    }

    [Test]
    public void Identify_EveryAllowedType_IsCovered()
    {
        EveryAllowedFile().Select(f => f.Type).Distinct().Should().BeEquivalentTo(AttachmentFileTypes.All);
    }

    [Test]
    public void Identify_ContentIsAPdfButNamedPng_RefusesWithTheRightExtension()
    {
        Identifying(Pdf, "scan.png").Should().Throw<UnsupportedFileTypeException>()
            .WithMessage("The file's name ends in .png, but it's a PDF. Name it with .pdf.");
    }

    [Test]
    public void Identify_HtmlNamedTxt_RefusesItAsAWebPage()
    {
        var html = Encoding.UTF8.GetBytes("﻿  \n<!DOCTYPE html><html><body>hi</body></html>");

        Identifying(html, "notes.txt").Should().Throw<UnsupportedFileTypeException>()
            .WithMessage("Web pages (HTML) can't be attached.*");
    }

    [TestCase("<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>")]
    [TestCase("<?xml version=\"1.0\"?>\n<SVG xmlns=\"http://www.w3.org/2000/svg\"/>")]
    public void Identify_Svg_RefusesItByName(string svg)
    {
        Identifying(Encoding.UTF8.GetBytes(svg), "image.svg").Should().Throw<UnsupportedFileTypeException>()
            .WithMessage("SVG images can't be attached.*");
    }

    [TestCase("<script>alert(1)</script>")]
    [TestCase("<iframe src=x></iframe>")]
    [TestCase("<head><title>x</title></head>")]
    public void Identify_HtmlFragments_AreWebPages(string html)
    {
        Identifying(Encoding.UTF8.GetBytes(html), "page.txt").Should().Throw<UnsupportedFileTypeException>()
            .WithMessage("Web pages (HTML)*");
    }

    [Test]
    public void Identify_OtherMarkup_IsRefusedAsMarkup()
    {
        Identifying(Encoding.UTF8.GetBytes("<?xml version=\"1.0\"?><results/>"), "results.txt")
            .Should().Throw<UnsupportedFileTypeException>().WithMessage("Markup files (XML or HTML)*");
    }

    [Test]
    public void Identify_TextMentioningMarkupLaterOn_IsText()
    {
        Identify(Encoding.UTF8.GetBytes("Patient letter: see <svg> tag in report."), "letter.txt")
            .Should().Be(AttachmentFileTypes.Text);
    }

    [Test]
    public void Identify_Empty_Refuses()
    {
        Identifying([], "empty.txt").Should().Throw<UnsupportedFileTypeException>().WithMessage("The file is empty.");
    }

    [TestCase("gif")]
    [TestCase("exe")]
    public void Identify_OtherBinaryFiles_AreRefused(string kind)
    {
        var bytes = kind == "gif" ? Gif : Executable;

        Identifying(bytes, $"file.{kind}").Should().Throw<UnsupportedFileTypeException>()
            .WithMessage("This kind of file can't be attached. Attach PDF, PNG, JPEG*");
    }

    [Test]
    public void Identify_TextWithAControlCharacter_IsRefused()
    {
        Identifying(Encoding.UTF8.GetBytes("abc\u0001def"), "notes.txt").Should().Throw<UnsupportedFileTypeException>();
    }

    [Test]
    public void Identify_InvalidUtf8_IsRefused()
    {
        Identifying([(byte)'a', 0xC3, 0x28, (byte)'b'], "notes.txt").Should().Throw<UnsupportedFileTypeException>();
    }

    [Test]
    public void Identify_Utf16Text_IsRefused()
    {
        Identifying(Encoding.Unicode.GetBytes("﻿hello"), "notes.txt").Should().Throw<UnsupportedFileTypeException>();
    }

    [Test]
    public void Identify_LongTextWithAMultiByteCharacterAcrossBlocks_IsText()
    {
        // 16 KB blocks: put a 3-byte character across the first boundary.
        var text = new string('a', 16 * 1024 - 1) + "€" + new string('b', 100);

        Identify(Encoding.UTF8.GetBytes(text), "long.txt").Should().Be(AttachmentFileTypes.Text);
    }

    [Test]
    public void Identify_TextEndingInHalfACharacter_IsRefused()
    {
        var bytes = Encoding.UTF8.GetBytes("abc").Concat(new byte[] { 0xE2, 0x82 }).ToArray();

        Identifying(bytes, "notes.txt").Should().Throw<UnsupportedFileTypeException>();
    }

    [Test]
    public void Identify_NoExtension_SaysWhatToNameIt()
    {
        Identifying(JpegHeader, "photo").Should().Throw<UnsupportedFileTypeException>()
            .WithMessage("The file's name has no extension. This file is a JPEG image, so name it with .jpg or .jpeg.");
    }

    [Test]
    public void Identify_Avif_IsRefused()
    {
        Identifying(IsoMedia("avif", "mif1", "avif"), "photo.heif").Should().Throw<UnsupportedFileTypeException>()
            .WithMessage("AVIF images*");
    }

    [Test]
    public void Identify_Video_IsRefused()
    {
        Identifying(IsoMedia("isom", "isom", "mp41"), "clip.heic").Should().Throw<UnsupportedFileTypeException>()
            .WithMessage("Videos and other media files*");
    }

    [Test]
    public void Identify_HeicAsMajorBrandOnly_IsHeic()
    {
        Identify(IsoMedia("heic"), "photo.heic").Should().Be(AttachmentFileTypes.Heic);
    }

    [Test]
    public void Identify_WordWithMacros_IsRefused()
    {
        Identifying(Docx(withMacros: true), "letter.docx").Should().Throw<UnsupportedFileTypeException>()
            .WithMessage("Word documents with macros*");
    }

    [Test]
    public void Identify_WordDeclaredMacroEnabled_IsRefused()
    {
        Identifying(Docx("application/vnd.ms-word.document.macroEnabled.main+xml"), "letter.docx")
            .Should().Throw<UnsupportedFileTypeException>().WithMessage("Word documents with macros*");
    }

    [Test]
    public void Identify_WordTemplate_IsRefused()
    {
        Identifying(Docx("application/vnd.openxmlformats-officedocument.wordprocessingml.template.main+xml"), "letter.docx")
            .Should().Throw<UnsupportedFileTypeException>().WithMessage("ZIP files and other Office documents*");
    }

    [Test]
    public void Identify_PlainZip_IsRefused()
    {
        Identifying(Zip(("readme.txt", "hello")), "archive.docx").Should().Throw<UnsupportedFileTypeException>()
            .WithMessage("ZIP files and other Office documents*");
    }

    [Test]
    public void Identify_SpreadsheetNamedDocx_IsRefused()
    {
        var xlsx = Zip(("[Content_Types].xml", "<Types/>"), ("xl/workbook.xml", "<workbook/>"));

        Identifying(xlsx, "sheet.docx").Should().Throw<UnsupportedFileTypeException>();
    }

    [Test]
    public void Identify_HugeContentTypesPart_IsRefusedWithoutReadingIt()
    {
        var huge = Zip(("[Content_Types].xml", new string(' ', FileTypeSniffer.MaxContentTypesBytes + 1)), ("word/document.xml", "<w/>"));

        Identifying(huge, "letter.docx").Should().Throw<UnsupportedFileTypeException>();
    }

    [Test]
    public void Identify_DamagedZip_SaysItIsDamaged()
    {
        var damaged = Docx().Take(40).ToArray();

        Identifying(damaged, "letter.docx").Should().Throw<UnsupportedFileTypeException>()
            .WithMessage("*damaged*");
    }

    [Test]
    public void Identify_StreamThatCantSeek_Throws()
    {
        var act = () => FileTypeSniffer.Identify(new NonSeekableStream(Pdf), "a.pdf");

        act.Should().Throw<ArgumentException>();
    }

    private sealed class NonSeekableStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override bool CanSeek => false;
    }
}
