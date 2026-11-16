using FluentAssertions;
using MedApp.Application.Attachments.Common;

namespace MedApp.UnitTests.Application.Attachments.Common;

[TestFixture]
public sealed class AttachmentFileNameTests
{
    [TestCase("scan.pdf", "scan.pdf")]
    [TestCase("  scan.pdf  ", "scan.pdf")]
    [TestCase("C:\\Users\\someone\\scan.pdf", "scan.pdf")]
    [TestCase("../../etc/passwd.txt", "passwd.txt")]
    [TestCase("scan\u0000\u001F.pdf", "scan.pdf")]
    [TestCase("invoice\u202Efdp.exe", "invoicefdp.exe")]
    [TestCase("a\u2066b\u2069c\u200E\u200F\u061C.txt", "abc.txt")]
    [TestCase("trailing.pdf. . .", "trailing.pdf")]
    public void Clean_Name_KeepsOnlyTheSafeName(string name, string expected)
    {
        AttachmentFileName.Clean(name).Should().Be(expected);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("folder/")]
    [TestCase("\u202E\u0001")]
    [TestCase("...")]
    public void Clean_NothingLeft_ReturnsNull(string? name)
    {
        AttachmentFileName.Clean(name).Should().BeNull();
    }

    [Test]
    public void Clean_DecomposedAccents_AreNormalised()
    {
        AttachmentFileName.Clean("re\u0301sume\u0301.pdf").Should().Be("résumé.pdf");
    }

    [Test]
    public void Clean_TooLong_ShortensKeepingTheExtension()
    {
        var cleaned = AttachmentFileName.Clean(new string('a', 400) + ".pdf")!;

        cleaned.Should().HaveLength(AttachmentLimits.MaxFileNameLength).And.EndWith(".pdf");
    }

    [Test]
    public void Clean_TooLongWithEmoji_NeverSplitsACharacter()
    {
        var cleaned = AttachmentFileName.Clean(string.Concat(Enumerable.Repeat("😀", 200)) + ".txt")!;

        cleaned.Length.Should().BeLessThanOrEqualTo(AttachmentLimits.MaxFileNameLength);
        cleaned.Should().EndWith(".txt");
        char.IsHighSurrogate(cleaned[^5]).Should().BeFalse();
    }

    [Test]
    public void Clean_TooLongWithAHugeExtension_CutsTheWhole()
    {
        var cleaned = AttachmentFileName.Clean("a." + new string('b', 400))!;

        cleaned.Should().HaveLength(AttachmentLimits.MaxFileNameLength).And.StartWith("a.bbb");
    }

    [TestCase("scan.PDF", ".pdf")]
    [TestCase("archive.tar.gz", ".gz")]
    [TestCase("noextension", "")]
    [TestCase(".hidden", "")]
    [TestCase("dot.", "")]
    public void Extension_Name_ReturnsItLowerCase(string name, string expected)
    {
        AttachmentFileName.Extension(name).Should().Be(expected);
    }

    [Test]
    public void ForDownload_CharactersWindowsRefuses_AreReplaced()
    {
        AttachmentFileName.ForDownload("a<b>c:d\"e|f?g*h.pdf", AttachmentFileTypes.Pdf).Should().Be("a_b_c_d_e_f_g_h.pdf");
    }

    [TestCase("")]
    [TestCase("noextension")]
    public void ForDownload_NoUsableName_FallsBackToFile(string name)
    {
        AttachmentFileName.ForDownload(name, AttachmentFileTypes.Jpeg).Should().Be("file.jpg");
    }

    [Test]
    public void ForDownload_UnknownType_FallsBackToPlainFile()
    {
        AttachmentFileName.ForDownload("", null).Should().Be("file");
    }

    [Test]
    public void AsciiFallback_NonAsciiQuotesAndBackslashes_BecomeUnderscores()
    {
        AttachmentFileName.AsciiFallback("résumé \"final\"\\.pdf").Should().Be("r_sum_ _final__.pdf");
    }
}
