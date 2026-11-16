using AutoFixture;
using FluentAssertions;
using MedApp.Application.Attachments.Common;
using MedApp.Application.Attachments.Queries.GetAttachmentThumbnail;
using MedApp.Application.Common.Exceptions;
using MedApp.UnitTests.Common.Fixtures;
using static MedApp.UnitTests.Common.Constants.AttachmentsTestConstants;

namespace MedApp.UnitTests.Application.Attachments.Queries.GetAttachmentThumbnail;

[TestFixture]
public sealed class GetAttachmentThumbnailHandlerTests : AttachmentHandlerTestBase
{
    private GetAttachmentThumbnailHandler Handler => Fixture.Create<GetAttachmentThumbnailHandler>();

    [TestCase("image/png", "image/png", "thumbnail.png")]
    [TestCase("image/jpeg", "image/jpeg", "thumbnail.jpg")]
    public async Task Handle_ImageWithAThumbnail_ReturnsItWithoutAuditing(string type, string thumbnailType, string name)
    {
        var attachment = GivenAttachment(CreateAttachment(hasThumbnail: true, contentType: type));
        await Store.SaveAsync(AttachmentStorageKeys.Thumbnail(attachment.Id), Stream([7, 8]), thumbnailType, CancellationToken.None);

        var thumbnail = await Handler.Handle(new GetAttachmentThumbnailQuery(attachment.Id), CancellationToken.None);

        thumbnail.ContentType.Should().Be(thumbnailType);
        thumbnail.FileName.Should().Be(name);
        thumbnail.Length.Should().BeNull();
        SavedAudit.Should().BeEmpty();
    }

    private static IEnumerable<TestCaseData> NoThumbnail()
    {
        yield return new TestCaseData(CreateAttachment(hasThumbnail: false, contentType: "image/png")).SetName("Handle_NoneMade_ThrowsNotFound");
        yield return new TestCaseData(CreateAttachment(hasThumbnail: true, contentType: "image/png", deleted: true)).SetName("Handle_Deleted_ThrowsNotFound");
        yield return new TestCaseData(CreateAttachment(hasThumbnail: true, contentType: "image/gif")).SetName("Handle_TypeNoLongerAllowed_ThrowsNotFound");
        yield return new TestCaseData(CreateAttachment(hasThumbnail: true, contentType: "image/png")).SetName("Handle_MissingFromStorage_ThrowsNotFound");
    }

    [TestCaseSource(nameof(NoThumbnail))]
    public async Task Handle_NoThumbnailToSend_ThrowsNotFound(MedApp.Domain.Attachments.Attachment attachment)
    {
        GivenAttachment(attachment);

        var act = () => Handler.Handle(new GetAttachmentThumbnailQuery(attachment.Id), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task Handle_Unknown_ThrowsNotFound()
    {
        var act = () => Handler.Handle(new GetAttachmentThumbnailQuery(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task Handle_StorageNotSetUp_ThrowsUnavailable()
    {
        Store.IsConfigured = false;

        var act = () => Handler.Handle(new GetAttachmentThumbnailQuery(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<FileStorageUnavailableException>();
    }
}
