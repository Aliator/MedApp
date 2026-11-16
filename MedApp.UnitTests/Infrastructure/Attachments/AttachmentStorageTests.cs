using FluentAssertions;
using MedApp.Application.Attachments.Common;
using MedApp.Domain.Attachments;
using MedApp.Infrastructure.Attachments;

namespace MedApp.UnitTests.Infrastructure.Attachments;

[TestFixture]
public sealed class AttachmentStorageTests
{
    [Test]
    public void UnconfiguredFileStore_IsNotConfiguredAndRefusesEverything()
    {
        var store = new UnconfiguredFileStore();

        store.IsConfigured.Should().BeFalse();
        FluentActions.Awaiting(() => store.SaveAsync("k", Stream.Null, "text/plain", CancellationToken.None))
            .Should().ThrowAsync<FileStorageUnavailableException>();
        FluentActions.Awaiting(() => store.OpenReadAsync("k", CancellationToken.None))
            .Should().ThrowAsync<FileStorageUnavailableException>();
        FluentActions.Awaiting(() => store.DeleteAsync("k", CancellationToken.None))
            .Should().ThrowAsync<FileStorageUnavailableException>();
    }

    [Test]
    public async Task NoMalwareScanner_SaysItDidNotScan()
    {
        (await new NoMalwareScanner().ScanAsync(Stream.Null, CancellationToken.None)).Should().Be(AttachmentScanStatus.NotScanned);
    }

    [Test]
    public async Task TempFileUploadStaging_IsASeekableTemporaryFileDeletedOnDispose()
    {
        string path;
        await using (var stream = new TempFileUploadStaging().Create())
        {
            stream.CanRead.Should().BeTrue();
            stream.CanWrite.Should().BeTrue();
            stream.CanSeek.Should().BeTrue();
            await stream.WriteAsync("synthetic"u8.ToArray());
            stream.Position = 0;
            stream.ReadByte().Should().Be('s');

            path = ((FileStream)stream).Name;
            Path.GetDirectoryName(path).Should().Be(Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar));
            File.Exists(path).Should().BeTrue();
        }

        File.Exists(path).Should().BeFalse();
    }

    [TestCase("DefaultEndpointsProtocol=http;AccountName=a;AccountKey=k;BlobEndpoint=http://h/a;",
        "DefaultEndpointsProtocol=http;AccountName=a;AccountKey=k;BlobEndpoint=http://h/a")]
    [TestCase("Endpoint=https://a.blob.core.windows.net/;ContainerName=attachments",
        "Endpoint=https://a.blob.core.windows.net/")]
    [TestCase("AccountName=a; containername=x ;AccountKey=k", "AccountName=a;AccountKey=k")]
    public void WithoutContainerName_DropsOnlyThatPart(string connectionString, string expected)
    {
        AttachmentStorageOptions.WithoutContainerName(connectionString).Should().Be(expected);
    }

    [Test]
    public void StorageKeys_AreMadeFromTheIdAlone()
    {
        var id = Guid.Parse("0000000f-0000-0000-0000-000000000009");

        AttachmentStorageKeys.File(id).Should().Be("files/0000000f000000000000000000000009");
        AttachmentStorageKeys.Thumbnail(id).Should().Be("thumbnails/0000000f000000000000000000000009");
    }

    [Test]
    public void ThumbnailContentType_PngForPngAndJpegOtherwise()
    {
        IThumbnailGenerator.ContentTypeOf(AttachmentFileTypes.Png).Should().Be("image/png");
        IThumbnailGenerator.ContentTypeOf(AttachmentFileTypes.Jpeg).Should().Be("image/jpeg");
    }
}
