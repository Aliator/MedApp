using FluentAssertions;
using MedApp.Application.Attachments.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Specimens.Commands.ReportSpecimen;
using MedApp.Domain.Attachments;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Specimens;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using static MedApp.UnitTests.Common.Constants.SpecimensTestConstants;
using Status = MedApp.Domain.Pathways.PatientPathways.PatientPathwayStatus;

namespace MedApp.UnitTests.Application.Specimens.Commands;

[TestFixture]
public sealed class ReportSpecimenHandlerTests : SpecimenTestBase
{
    private InMemoryFileStore _store = null!;
    private Mock<IMalwareScanner> _scanner = null!;

    [SetUp]
    public void SetUpFiles()
    {
        _store = new InMemoryFileStore();
        _scanner = new Mock<IMalwareScanner>();
        _scanner.Setup(s => s.ScanAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>())).ReturnsAsync(AttachmentScanStatus.NotScanned);
    }

    private ReportSpecimenHandler Handler => new(
        Repository.Object, _store, new MemoryStaging(), _scanner.Object, Mock.Of<IThumbnailGenerator>(),
        Identity.Object, QrCodes.Object, Notifications.Object, Boundary.Object, Teams.Object, Clock);

    private static ReportSpecimenCommand Command(Specimen specimen, byte[]? file = null, string name = "report.pdf", int version = 3) =>
        new(specimen.Id, name, null, new MemoryStream(file ?? AttachmentsTestConstants.Pdf), version, LabStaff);

    [Test]
    public async Task Handle_Received_AttachesTheReportToThePathwayAndReportsIt_InOneSave()
    {
        var specimen = Given(CreateSpecimen(status: SpecimenStatus.Received));

        var response = await Handler.Handle(Command(specimen), Cts.Token);

        var report = SavedReports.Should().ContainSingle().Subject;
        report.PathwayId.Should().Be(specimen.PathwayId);
        report.PatientId.Should().Be(specimen.PatientId);
        report.ContentType.Should().Be("application/pdf");
        report.UploadedByUserId.Should().Be(LabStaff.UserId);
        _store.Blobs.Should().ContainKey(AttachmentStorageKeys.File(report.Id));
        specimen.Status.Should().Be(SpecimenStatus.Reported);
        specimen.ReportAttachmentId.Should().Be(report.Id);
        specimen.ReportedByUserId.Should().Be(LabStaff.UserId);
        SavedAudit.Select(e => e.Action).Should().Equal(
            PatientPathwayAuditAction.AttachmentAdded, PatientPathwayAuditAction.SpecimenReported, PatientPathwayAuditAction.ResultRecorded);
        Tokens.Should().Equal(Cts.Token);
        response.ReportAttachmentId.Should().Be(report.Id);
    }

    [Test]
    public async Task Handle_Received_MakesAResultForTheRequestingClinician_AndTellsThem()
    {
        var specimen = Given(CreateSpecimen(status: SpecimenStatus.Received));

        await Handler.Handle(Command(specimen), Cts.Token);

        var result = SavedResults.Should().ContainSingle().Subject;
        result.Source.Should().Be(MedApp.Domain.Results.ResultSource.Specimen);
        result.Status.Should().Be(MedApp.Domain.Results.ResultStatus.AwaitingSignOff);
        result.SpecimenId.Should().Be(specimen.Id);
        result.PathwayId.Should().Be(specimen.PathwayId);
        result.ReportAttachmentId.Should().Be(SavedReports.Single().Id);
        result.ResponsibleUserId.Should().Be(specimen.RequestingUserId);
        result.ReportedByUserId.Should().Be(LabStaff.UserId);
        result.Version.Should().Be(1);
        result.Title.Should().Contain(specimen.Number);
        var ready = Sent.Should().ContainSingle().Subject;
        ready.Kind.Should().Be(MedApp.Domain.Notifications.NotificationKind.ResultReady);
        ready.UserId.Should().Be(specimen.RequestingUserId);
        ready.SubjectId.Should().Be(result.Id);
        ready.Message.Should().NotContain(specimen.Number);
    }

    [Test]
    public async Task Handle_PathwayCancelledMeanwhile_AttachesItToThePatient()
    {
        var specimen = Given(CreateSpecimen(CreatePathway(Status.Cancelled), SpecimenStatus.Received));

        await Handler.Handle(Command(specimen), Cts.Token);

        SavedReports.Single().PathwayId.Should().BeNull();
        SavedAudit.First().PatientPathwayId.Should().Be(PatientPathwayAuditEntry.NoPathway);
    }

    [Test]
    public async Task Handle_RestrictedRecord_StillReportsIt()
    {
        var specimen = Given(CreateSpecimen(status: SpecimenStatus.Received));
        specimen.Patient.RestrictedAtUtc = Now;

        await Handler.Handle(Command(specimen), Cts.Token);

        specimen.Status.Should().Be(SpecimenStatus.Reported);
    }

    [Test]
    public async Task Handle_NotReceived_Throws409_AndStoresNothing()
    {
        var specimen = Given(CreateSpecimen(status: SpecimenStatus.Sent));

        var act = () => Handler.Handle(Command(specimen), Cts.Token);

        await act.Should().ThrowAsync<ConflictException>();
        _store.Blobs.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_StaleVersion_Throws409()
    {
        var specimen = Given(CreateSpecimen(status: SpecimenStatus.Received));

        var act = () => Handler.Handle(Command(specimen, version: 2), Cts.Token);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Test]
    public async Task Handle_HtmlFile_IsRefusedAsAnyAttachmentIs()
    {
        var specimen = Given(CreateSpecimen(status: SpecimenStatus.Received));

        var act = () => Handler.Handle(Command(specimen, "<html>synthetic</html>"u8.ToArray(), "report.html"), Cts.Token);

        await act.Should().ThrowAsync<UnsupportedFileTypeException>();
        specimen.Status.Should().Be(SpecimenStatus.Received);
        SavedReports.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_SaveFails_DeletesTheStoredBytes()
    {
        var specimen = Given(CreateSpecimen(status: SpecimenStatus.Received));
        Repository
            .Setup(r => r.SaveReportAsync(It.IsAny<Attachment>(), It.IsAny<MedApp.Domain.Results.Result>(), It.IsAny<IEnumerable<PatientPathwayAuditEntry>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConflictException("changed"));

        var act = () => Handler.Handle(Command(specimen), Cts.Token);

        await act.Should().ThrowAsync<ConflictException>();
        _store.Blobs.Should().BeEmpty();
        _store.Deleted.Should().ContainSingle();
    }

    [Test]
    public async Task Handle_NoFileStorage_Throws503()
    {
        var specimen = Given(CreateSpecimen(status: SpecimenStatus.Received));
        _store.IsConfigured = false;

        var act = () => Handler.Handle(Command(specimen), Cts.Token);

        await act.Should().ThrowAsync<FileStorageUnavailableException>();
    }

    [Test]
    public void Validator_NoFileNameOrVersion_IsRefused()
    {
        new ReportSpecimenValidator().Validate(new ReportSpecimenCommand(Guid.NewGuid(), " ", null, Stream.Null, 0, LabStaff))
            .Errors.Select(e => e.PropertyName).Should().BeEquivalentTo(["FileName", "ExpectedVersion"]);
    }

    private sealed class MemoryStaging : IUploadStaging
    {
        public Stream Create() => new MemoryStream();
    }
}
