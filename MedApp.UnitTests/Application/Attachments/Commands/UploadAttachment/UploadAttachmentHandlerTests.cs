using System.Security.Cryptography;
using System.Text;
using AutoFixture;
using FluentAssertions;
using MedApp.Application.Attachments.Commands.UploadAttachment;
using MedApp.Application.Attachments.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Attachments;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.AttachmentsTestConstants;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using Status = MedApp.Domain.Pathways.PatientPathways.PatientPathwayStatus;

namespace MedApp.UnitTests.Application.Attachments.Commands.UploadAttachment;

[TestFixture]
public sealed class UploadAttachmentHandlerTests : AttachmentHandlerTestBase
{
    private UploadAttachmentHandler Handler => Fixture.Create<UploadAttachmentHandler>();

    private static UploadAttachmentCommand Upload(
        byte[] bytes,
        Guid? patientId = null,
        Guid? pathwayId = null,
        Guid? stageId = null,
        string fileName = FileName,
        long? contentLength = null,
        bool isAdmin = false) =>
        new(patientId, pathwayId, stageId, null, fileName, contentLength ?? bytes.Length, Stream(bytes), Staff, isAdmin);

    [Test]
    public async Task Handle_PdfOnThePatient_StoresItRecordsItAndAuditsIt()
    {
        var patient = GivenPatient();
        using var cts = new CancellationTokenSource();

        var response = await Handler.Handle(Upload(Pdf, patientId: patient.Id), cts.Token);

        var attachment = Added.Should().ContainSingle().Subject;
        attachment.Should().BeEquivalentTo(new
        {
            PatientId = patient.Id,
            PathwayId = (Guid?)null,
            StageId = (Guid?)null,
            FileName,
            ContentType = "application/pdf",
            SizeBytes = (long)Pdf.Length,
            Sha256 = Convert.ToHexStringLower(SHA256.HashData(Pdf)),
            ScanStatus = AttachmentScanStatus.NotScanned,
            HasThumbnail = false,
            UploadedByUserId = Staff.UserId,
            UploadedByUsername = Staff.Username,
            UploadedAtUtc = Now,
        });
        var stored = Store.Blobs[AttachmentStorageKeys.File(attachment.Id)];
        stored.Bytes.Should().Equal(Pdf);
        stored.ContentType.Should().Be("application/pdf");
        SavedAudit.Should().ContainSingle(e => e.Action == PatientPathwayAuditAction.AttachmentAdded
                                               && e.PatientPathwayId == PatientPathwayAuditEntry.NoPathway
                                               && e.PatientId == patient.Id && e.AtUtc == Now);
        Attachments.Verify(a => a.AddAsync(attachment, It.IsAny<PatientPathwayAuditEntry>(), cts.Token), Times.Once);

        response.Id.Should().Be(attachment.Id);
        response.AttachedTo.Should().Be("Patient");
        response.CanDelete.Should().BeTrue();
    }

    [Test]
    public async Task Handle_FileOnThePathway_RecordsThePathway()
    {
        var pathway = GivenPathway(CreatePathway(Status.Completed));

        var response = await Handler.Handle(Upload(Text, pathwayId: pathway.Id, fileName: "letter.txt"), CancellationToken.None);

        Added.Single().PathwayId.Should().Be(pathway.Id);
        Added.Single().PatientId.Should().Be(pathway.PatientId);
        Added.Single().ContentType.Should().Be("text/plain; charset=utf-8");
        response.PathwayTitle.Should().Be(PathwayTitle);
        SavedAudit.Single().PatientPathwayId.Should().Be(pathway.Id);
    }

    [Test]
    public async Task Handle_FileOnTheCurrentStage_RecordsTheStage()
    {
        var pathway = CreatePathway(Status.InProgress, finishedStages: 1);
        var stage = GivenStage(pathway, 2);

        var response = await Handler.Handle(Upload(Pdf, stageId: stage.Id), CancellationToken.None);

        Added.Single().StageId.Should().Be(stage.Id);
        Added.Single().PathwayId.Should().Be(pathway.Id);
        response.StageName.Should().Be(PathwayStage);
        SavedAudit.Single().StageId.Should().Be(stage.Id);
    }

    [Test]
    public async Task Handle_FinishedStage_ThrowsConflictBeforeReadingTheFile()
    {
        var pathway = CreatePathway(Status.InProgress, finishedStages: 1);
        var stage = GivenStage(pathway, 1);
        var body = new Mock<Stream>(MockBehavior.Strict);

        var act = () => Handler.Handle(new UploadAttachmentCommand(null, null, stage.Id, null, FileName, 10, body.Object, Staff, false), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*finished and locked*");
        Store.Blobs.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_CancelledPathway_ThrowsConflict()
    {
        var pathway = GivenPathway(CreatePathway(Status.Cancelled));

        var act = () => Handler.Handle(Upload(Pdf, pathwayId: pathway.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Test]
    public async Task Handle_UnknownPatient_ThrowsNotFound()
    {
        var act = () => Handler.Handle(Upload(Pdf, patientId: Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage("Patient*");
    }

    [Test]
    public async Task Handle_UnknownPathway_ThrowsNotFound()
    {
        var act = () => Handler.Handle(Upload(Pdf, pathwayId: Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage("Pathway*");
    }

    [Test]
    public async Task Handle_UnknownStage_ThrowsNotFound()
    {
        var act = () => Handler.Handle(Upload(Pdf, stageId: Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage("Stage*");
    }

    [Test]
    public async Task Handle_StorageNotSetUp_ThrowsUnavailable()
    {
        Store.IsConfigured = false;
        var patient = GivenPatient();

        var act = () => Handler.Handle(Upload(Pdf, patientId: patient.Id), CancellationToken.None);

        await act.Should().ThrowAsync<FileStorageUnavailableException>();
    }

    [Test]
    public async Task Handle_DeclaredLengthOverTheLimit_ThrowsTooLargeBeforeLookingAnythingUp()
    {
        var act = () => Handler.Handle(Upload(Pdf, patientId: Guid.NewGuid(), contentLength: AttachmentLimits.MaxFileBytes + 1), CancellationToken.None);

        await act.Should().ThrowAsync<FileTooLargeException>().WithMessage("The file is larger than 20 MB*");
        Patients.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Handle_BodyLongerThanTheLimit_ThrowsTooLargeWhateverItsDeclaredLength()
    {
        var patient = GivenPatient();
        var bytes = new byte[AttachmentLimits.MaxFileBytes + 1];
        Pdf.CopyTo(bytes, 0);

        var act = () => Handler.Handle(Upload(bytes, patientId: patient.Id, contentLength: 100), CancellationToken.None);

        await act.Should().ThrowAsync<FileTooLargeException>();
        Store.Blobs.Should().BeEmpty();
        Added.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_ExactlyTheLimit_IsAccepted()
    {
        var patient = GivenPatient();
        var bytes = new byte[AttachmentLimits.MaxFileBytes];
        Pdf.CopyTo(bytes, 0);

        await Handler.Handle(Upload(bytes, patientId: patient.Id, contentLength: null), CancellationToken.None);

        Added.Single().SizeBytes.Should().Be(AttachmentLimits.MaxFileBytes);
    }

    [Test]
    public async Task Handle_Svg_IsRefusedAndNothingIsStored()
    {
        var patient = GivenPatient();

        var act = () => Handler.Handle(Upload(Encoding.UTF8.GetBytes("<svg/>"), patientId: patient.Id, fileName: "x.svg"), CancellationToken.None);

        await act.Should().ThrowAsync<UnsupportedFileTypeException>().WithMessage("SVG images*");
        Store.Blobs.Should().BeEmpty();
        SavedAudit.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_MalwareFound_IsRefusedAndNothingIsStored()
    {
        var patient = GivenPatient();
        Scanner.Setup(s => s.ScanAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>())).ReturnsAsync(AttachmentScanStatus.Infected);

        var act = () => Handler.Handle(Upload(Pdf, patientId: patient.Id), CancellationToken.None);

        await act.Should().ThrowAsync<MalwareDetectedException>();
        Store.Blobs.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_ScannedClean_RecordsIt()
    {
        var patient = GivenPatient();
        Scanner.Setup(s => s.ScanAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>())).ReturnsAsync(AttachmentScanStatus.Clean);

        var response = await Handler.Handle(Upload(Pdf, patientId: patient.Id), CancellationToken.None);

        response.ScanStatus.Should().Be("Clean");
    }

    [Test]
    public async Task Handle_Image_StoresItsThumbnail()
    {
        var patient = GivenPatient();
        byte[] thumbnail = [1, 2, 3];
        Thumbnails
            .Setup(t => t.CreateAsync(It.IsAny<Stream>(), AttachmentFileTypes.Png, It.IsAny<CancellationToken>()))
            .ReturnsAsync(thumbnail);

        var response = await Handler.Handle(Upload(PngHeader, patientId: patient.Id, fileName: "photo.png"), CancellationToken.None);

        response.HasThumbnail.Should().BeTrue();
        var stored = Store.Blobs[AttachmentStorageKeys.Thumbnail(response.Id)];
        stored.Bytes.Should().Equal(thumbnail);
        stored.ContentType.Should().Be("image/png");
    }

    [Test]
    public async Task Handle_ImageWithoutAThumbnail_StillAttachesIt()
    {
        var patient = GivenPatient();

        var response = await Handler.Handle(Upload(JpegHeader, patientId: patient.Id, fileName: "photo.jpg"), CancellationToken.None);

        response.HasThumbnail.Should().BeFalse();
        Store.Blobs.Keys.Should().ContainSingle();
    }

    [Test]
    public async Task Handle_Pdf_NeverAsksForAThumbnail()
    {
        var patient = GivenPatient();

        await Handler.Handle(Upload(Pdf, patientId: patient.Id), CancellationToken.None);

        Thumbnails.Verify(t => t.CreateAsync(It.IsAny<Stream>(), It.IsAny<AttachmentFileType>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_SavingTheRecordFails_DeletesTheStoredFileAndThumbnail()
    {
        var patient = GivenPatient();
        Thumbnails
            .Setup(t => t.CreateAsync(It.IsAny<Stream>(), It.IsAny<AttachmentFileType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([1]);
        Attachments
            .Setup(a => a.AddAsync(It.IsAny<Attachment>(), It.IsAny<PatientPathwayAuditEntry>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Synthetic database failure."));

        var act = () => Handler.Handle(Upload(PngHeader, patientId: patient.Id, fileName: "photo.png"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        Store.Blobs.Should().BeEmpty();
        Store.Deleted.Should().HaveCount(2);
    }

    [Test]
    public async Task Handle_StoringTheThumbnailFails_DeletesTheFileEvenIfThatFailsAndRethrows()
    {
        var patient = GivenPatient();
        Thumbnails
            .Setup(t => t.CreateAsync(It.IsAny<Stream>(), It.IsAny<AttachmentFileType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([1]);
        var failing = new Mock<IFileStore>();
        failing.SetupGet(s => s.IsConfigured).Returns(true);
        failing.Setup(s => s.SaveAsync(It.Is<string>(k => k.StartsWith("thumbnails/")), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("Synthetic storage failure."));
        failing.Setup(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("Deleting fails too."));
        Fixture.Inject(failing.Object);

        var act = () => Handler.Handle(Upload(PngHeader, patientId: patient.Id, fileName: "photo.png"), CancellationToken.None);

        await act.Should().ThrowAsync<IOException>().WithMessage("Synthetic storage failure.");
        // Not the request's token, but a bounded one of its own.
        failing.Verify(s => s.DeleteAsync(It.Is<string>(k => k.StartsWith("files/")), It.Is<CancellationToken>(t => t.CanBeCanceled)), Times.Once);
        Added.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_FileName_IsCleanedBeforeItIsKept()
    {
        var patient = GivenPatient();

        await Handler.Handle(Upload(Pdf, patientId: patient.Id, fileName: "C:\\scans\\re\u202Eport.pdf"), CancellationToken.None);

        Added.Single().FileName.Should().Be("report.pdf");
    }

    [Test]
    public async Task Handle_Admin_CanDeleteWhatTheyAttach()
    {
        var patient = GivenPatient();

        var response = await Handler.Handle(Upload(Pdf, patientId: patient.Id, isAdmin: true), CancellationToken.None);

        response.CanDelete.Should().BeTrue();
    }
}
