using AutoFixture;
using FluentAssertions;
using MedApp.Application.Attachments.Commands.DownloadAttachment;
using MedApp.Application.Attachments.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Procedures.PatientProcedures;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.AttachmentsTestConstants;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;

namespace MedApp.UnitTests.Application.Attachments.Commands.DownloadAttachment;

[TestFixture]
public sealed class DownloadAttachmentHandlerTests : AttachmentHandlerTestBase
{
    private DownloadAttachmentHandler Handler => Fixture.Create<DownloadAttachmentHandler>();

    private async Task<MedApp.Domain.Attachments.Attachment> StoredAttachment(MedApp.Domain.Attachments.Attachment attachment)
    {
        GivenAttachment(attachment);
        await Store.SaveAsync(AttachmentStorageKeys.File(attachment.Id), Stream(Pdf), attachment.ContentType, CancellationToken.None);
        return attachment;
    }

    [Test]
    public async Task Handle_LiveFile_ReturnsItsBytesNameAndTypeAndAuditsTheDownload()
    {
        var procedure = GivenProcedure(CreateProcedure());
        var attachment = await StoredAttachment(AttachmentOn(procedure, stageOrder: 2));
        using var cts = new CancellationTokenSource();

        var download = await Handler.Handle(new DownloadAttachmentCommand(attachment.Id, Staff), cts.Token);

        download.FileName.Should().Be(FileName);
        download.ContentType.Should().Be("application/pdf");
        download.Length.Should().Be(attachment.SizeBytes);
        using (var copy = new MemoryStream())
        {
            await download.Content.CopyToAsync(copy);
            copy.ToArray().Should().Equal(Pdf);
        }

        SavedAudit.Should().ContainSingle(e => e.Action == PatientProcedureAuditAction.AttachmentDownloaded
                                               && e.UserId == Staff.UserId && e.PatientProcedureId == procedure.Id
                                               && e.StageName == ProcedureStage);
        Attachments.Verify(a => a.AddAuditAsync(It.IsAny<PatientProcedureAuditEntry>(), cts.Token), Times.Once);
    }

    [Test]
    public async Task Handle_NameWindowsRefuses_IsMadeSafeForSaving()
    {
        var attachment = await StoredAttachment(CreateAttachment(fileName: "a:b.pdf"));

        var download = await Handler.Handle(new DownloadAttachmentCommand(attachment.Id, Staff), CancellationToken.None);

        download.FileName.Should().Be("a_b.pdf");
    }

    [Test]
    public async Task Handle_DeletedFile_ThrowsNotFoundEvenForAnAdmin()
    {
        var attachment = await StoredAttachment(CreateAttachment(deleted: true));

        var act = () => Handler.Handle(new DownloadAttachmentCommand(attachment.Id, Admin), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        SavedAudit.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_Unknown_ThrowsNotFound()
    {
        var act = () => Handler.Handle(new DownloadAttachmentCommand(Guid.NewGuid(), Staff), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task Handle_BytesMissingFromStorage_ThrowsNotFound()
    {
        var attachment = GivenAttachment(CreateAttachment());

        var act = () => Handler.Handle(new DownloadAttachmentCommand(attachment.Id, Staff), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage("*no contents*");
        SavedAudit.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_StorageNotSetUp_ThrowsUnavailable()
    {
        Store.IsConfigured = false;

        var act = () => Handler.Handle(new DownloadAttachmentCommand(Guid.NewGuid(), Staff), CancellationToken.None);

        await act.Should().ThrowAsync<FileStorageUnavailableException>();
    }

    [Test]
    public async Task Handle_AuditFails_ClosesTheStreamAndRethrows()
    {
        var attachment = GivenAttachment(CreateAttachment());
        var content = new TrackingStream();
        var store = new Mock<IFileStore>();
        store.SetupGet(s => s.IsConfigured).Returns(true);
        store.Setup(s => s.OpenReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(content);
        Fixture.Inject(store.Object);
        Attachments.Setup(a => a.AddAuditAsync(It.IsAny<PatientProcedureAuditEntry>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Synthetic database failure."));

        var act = () => Handler.Handle(new DownloadAttachmentCommand(attachment.Id, Staff), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        content.Disposed.Should().BeTrue();
    }

    private sealed class TrackingStream : MemoryStream
    {
        public bool Disposed { get; private set; }

        public override ValueTask DisposeAsync()
        {
            Disposed = true;
            return base.DisposeAsync();
        }
    }
}
