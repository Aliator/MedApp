using AutoFixture;
using FluentAssertions;
using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Patients.Common;
using MedApp.Application.Patients.Queries.ExportPatient;
using MedApp.Application.Patients.Repositories;
using MedApp.Domain.Activity;
using MedApp.Domain.Attachments;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;

namespace MedApp.UnitTests.Application.Patients.Queries.ExportPatient;

[TestFixture]
public sealed class ExportPatientHandlerTests
{
    private IFixture _fixture = null!;
    private Mock<IPatientRecordRepository> _repository = null!;
    private Mock<IIdentityReadService> _identity = null!;
    private RecordingActivityLog _activity = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();
        _repository = new Mock<IPatientRecordRepository>();
        _identity = new Mock<IIdentityReadService>();
        _identity
            .Setup(i => i.GetUsernamesByIdAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Usernames);
        _activity = new RecordingActivityLog();
        _fixture.Inject(_repository.Object);
        _fixture.Inject(_identity.Object);
        _fixture.Inject<IActivityLog>(_activity);
        _fixture.Inject<TimeProvider>(new FixedTimeProvider(ActivityTestConstants.Now));
    }

    [Test]
    public async Task Handle_UnknownPatient_ThrowsNotFound_AndRecordsNothing()
    {
        var act = () => _fixture.Create<ExportPatientHandler>().Handle(new ExportPatientQuery(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        _activity.Recorded.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_Patient_RecordsTheExport_AndIncludesItInTheAccessLog()
    {
        var record = PrivacyTestConstants.CreateRecord(null, CreatePathway());
        _repository.Setup(r => r.GetAsync(record.Patient.Id, false, It.IsAny<CancellationToken>())).ReturnsAsync(record);
        using var cts = new CancellationTokenSource();

        var export = await _fixture.Create<ExportPatientHandler>().Handle(new ExportPatientQuery(record.Patient.Id), cts.Token);

        var entry = _activity.Recorded.Should().ContainSingle().Subject;
        entry.Action.Should().Be(ActivityAction.PatientExported);
        entry.PatientId.Should().Be(record.Patient.Id);
        export.AccessLog.Should().HaveCount(2);
        export.AccessLog.Last().Action.Should().Be(nameof(ActivityAction.PatientExported));
        export.AccessLog.Last().AtUtc.Should().Be(entry.AtUtc);
        export.ExportedBy.Should().Be(ActivityTestConstants.Username);
        export.ExportedAtUtc.Should().Be(ActivityTestConstants.Now);
        export.Patient.Id.Should().Be(record.Patient.Id);
        export.Pathways.Should().ContainSingle();
        export.Notifications.Single().Username.Should().Be(Staff.Username);
        _activity.Tokens.Should().Equal(cts.Token);
        _repository.Verify(r => r.GetAsync(record.Patient.Id, false, cts.Token), Times.Once);
        _identity.Verify(i => i.GetUsernamesByIdAsync(
            It.Is<IEnumerable<Guid>>(ids => ids.ToHashSet().SetEquals(PatientExportMapper.UserIds(record))), cts.Token), Times.Once);
    }

    [Test]
    public async Task Handle_EveryListInTheRecord_IsInTheExport()
    {
        // The handler copies the record to add the export's own access entry: no list may be lost in the copy.
        var pathway = CreatePathway();
        var note = pathway.AddNote();
        var record = PrivacyTestConstants.CreateRecord(null, pathway);
        record = record with
        {
            NoteVersions =
            [
                new PatientPathwayNoteVersion
                {
                    Id = Guid.NewGuid(), NoteId = note.Id, Version = 1, PatientPathwayId = pathway.Id, PatientId = record.Patient.Id,
                    Text = NoteText, UserId = Staff.UserId, Username = Staff.Username, AtUtc = Now
                }
            ],
            NoteDeletionRequests = [pathway.RequestDeletion(note)],
            Attachments =
            [
                new Attachment
                {
                    Id = Guid.NewGuid(), PatientId = record.Patient.Id, FileName = "synthetic.pdf", ContentType = "application/pdf",
                    UploadedAtUtc = Now, UploadedByUserId = Staff.UserId, UploadedByUsername = Staff.Username
                }
            ]
        };
        _repository.Setup(r => r.GetAsync(record.Patient.Id, false, It.IsAny<CancellationToken>())).ReturnsAsync(record);

        var export = await _fixture.Create<ExportPatientHandler>().Handle(new ExportPatientQuery(record.Patient.Id), CancellationToken.None);

        export.NoteHistory.Should().ContainSingle().Which.NoteId.Should().Be(note.Id);
        export.NoteDeletionRequests.Should().ContainSingle().Which.NoteId.Should().Be(note.Id);
        export.Attachments.Should().ContainSingle().Which.FileName.Should().Be("synthetic.pdf");
        export.Appointments.Should().HaveCount(record.Appointments.Count);
        export.AppointmentHistory.Should().HaveCount(record.AppointmentHistory.Count);
        export.PathwayHistory.Should().HaveCount(record.PathwayHistory.Count);
    }
}
