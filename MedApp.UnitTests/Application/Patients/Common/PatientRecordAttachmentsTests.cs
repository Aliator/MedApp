using FluentAssertions;
using MedApp.Application.Attachments.Common;
using MedApp.Application.Patients.Common;
using MedApp.Domain.Attachments;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.UnitTests.Common.Constants;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using Status = MedApp.Domain.Pathways.PatientPathways.PatientPathwayStatus;

namespace MedApp.UnitTests.Application.Patients.Common;

/// <summary>A patient's attached files in their subject access export, and what anonymising them does.</summary>
[TestFixture]
public sealed class PatientRecordAttachmentsTests
{
    private const string Canary = "file-canary-3c9d";
    private static readonly DateTime AnonymisedAt = new(2026, 11, 16, 9, 0, 0, DateTimeKind.Utc);

    private static (PatientRecord Record, Attachment Live, Attachment Deleted, PatientPathway Pathway) RecordWithFiles()
    {
        var pathway = CreatePathway(Status.Completed, finishedStages: 3);
        var record = PrivacyTestConstants.CreateRecord(null, pathway);
        var live = new Attachment
        {
            Id = Guid.NewGuid(),
            PatientId = record.Patient.Id,
            PathwayId = pathway.Id,
            StageId = pathway.StageAt(2).Id,
            FileName = $"{Canary} scan.pdf",
            ContentType = "application/pdf",
            SizeBytes = 2048,
            Sha256 = new string('b', 64),
            ScanStatus = AttachmentScanStatus.NotScanned,
            UploadedByUserId = Staff.UserId,
            UploadedByUsername = Staff.Username,
            UploadedAtUtc = Now.AddDays(-2),
        };
        var deleted = new Attachment
        {
            Id = Guid.NewGuid(),
            PatientId = record.Patient.Id,
            FileName = $"{Canary}.jpg",
            ContentType = "image/jpeg",
            UploadedByUsername = Staff.Username,
            UploadedAtUtc = Now.AddDays(-1),
            DeletedAtUtc = Now.AddHours(-3),
            DeletedByUserId = Admin.UserId,
            DeletedByUsername = Admin.Username,
        };
        var audit = AttachmentAudit.Entry(live, new AttachmentTarget(record.Patient.Id, pathway, pathway.StageAt(2)), Staff,
            PatientPathwayAuditAction.AttachmentAdded, Now.AddDays(-2));

        return (new PatientRecord
        {
            Patient = record.Patient,
            Pathways = record.Pathways,
            PathwayHistory = [.. record.PathwayHistory, audit],
            Activity = record.Activity,
            Appointments = record.Appointments,
            AppointmentHistory = record.AppointmentHistory, PatientMessages = record.PatientMessages,
            Notifications = record.Notifications,
            Attachments = [live, deleted],
            NoteVersions = [],
            NoteDeletionRequests = [],
            Referrals = [],
            ReferralHistory = []
        }, live, deleted, pathway);
    }

    [Test]
    public void Export_ListsEveryFilesDetails_DeletedOnesToo()
    {
        var (record, live, deleted, pathway) = RecordWithFiles();

        var export = PatientExportMapper.ToResponse(record, new Dictionary<Guid, string>(), Now, "exporter");

        export.Attachments.Should().HaveCount(2);
        export.Attachments[0].Should().BeEquivalentTo(new
        {
            live.Id,
            AttachedTo = "Stage",
            PathwayId = (Guid?)pathway.Id,
            StageName = PathwayStage,
            live.FileName,
            ContentType = "application/pdf",
            TypeName = "PDF",
            SizeBytes = 2048L,
            live.Sha256,
            ScanStatus = "NotScanned",
            UploadedBy = Staff.Username,
            live.UploadedAtUtc,
            DeletedAtUtc = (DateTime?)null,
            DeletedBy = (string?)null,
        });
        export.Attachments[1].AttachedTo.Should().Be("Patient");
        export.Attachments[1].StageName.Should().BeNull();
        export.Attachments[1].DeletedBy.Should().Be(Admin.Username);
        export.Attachments[1].DeletedAtUtc.Should().Be(deleted.DeletedAtUtc);
    }

    [Test]
    public void Anonymise_RenamesEveryFile_KeepingItsExtension()
    {
        var (record, live, deleted, _) = RecordWithFiles();

        PatientAnonymiser.Apply(record, AnonymisedAt, ActivityTestConstants.Actor);

        live.FileName.Should().Be("Anonymised file.pdf");
        deleted.FileName.Should().Be("Anonymised file.jpg");
    }

    [Test]
    public void Anonymise_DeletesTheLiveFilesByWhoeverAnonymises_AndLeavesDeletedOnesAsTheyWere()
    {
        var (record, live, deleted, _) = RecordWithFiles();

        PatientAnonymiser.Apply(record, AnonymisedAt, ActivityTestConstants.Actor);

        live.DeletedAtUtc.Should().Be(AnonymisedAt);
        live.DeletedByUserId.Should().Be(ActivityTestConstants.Actor.UserId);
        live.DeletedByUsername.Should().Be(ActivityTestConstants.Actor.Username);
        deleted.DeletedAtUtc.Should().Be(Now.AddHours(-3));
        deleted.DeletedByUsername.Should().Be(Admin.Username);
    }

    [Test]
    public void Anonymise_WithoutSayingWho_StillDeletesTheFiles()
    {
        var (record, live, _, _) = RecordWithFiles();

        PatientAnonymiser.Apply(record, AnonymisedAt);

        live.IsDeleted.Should().BeTrue();
        live.DeletedByUsername.Should().BeNull();
    }

    [Test]
    public void Anonymise_FileNamesInTheAudit_AreRemoved()
    {
        var (record, _, _, _) = RecordWithFiles();

        PatientAnonymiser.Apply(record, AnonymisedAt);

        record.PathwayHistory.Should().AllSatisfy(e => e.Details.Should().NotContain(Canary));
        record.PathwayHistory.Single(e => e.Action == PatientPathwayAuditAction.AttachmentAdded).Details
            .Should().Contain($"\"fileName\":\"{PatientAnonymiser.RemovedText}\"").And.Contain("\"contentType\":\"application/pdf\"");
    }

    [Test]
    public void Anonymise_UnknownType_GetsNoExtension()
    {
        var (record, live, _, _) = RecordWithFiles();
        var odd = new Attachment { Id = Guid.NewGuid(), FileName = Canary, ContentType = "image/gif" };
        var withOdd = new PatientRecord
        {
            Patient = record.Patient, Pathways = record.Pathways, PathwayHistory = record.PathwayHistory,
            Activity = record.Activity, Appointments = record.Appointments, AppointmentHistory = record.AppointmentHistory, PatientMessages = record.PatientMessages,
            Notifications = record.Notifications, Attachments = [live, odd],
            NoteVersions = [],
            NoteDeletionRequests = [],
            Referrals = [],
            ReferralHistory = []
        };

        PatientAnonymiser.Apply(withOdd, AnonymisedAt);

        odd.FileName.Should().Be(PatientAnonymiser.FileName);
    }
}
