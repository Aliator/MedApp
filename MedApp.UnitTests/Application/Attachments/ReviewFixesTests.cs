using System.Text.Json;
using AutoFixture;
using FluentAssertions;
using MedApp.API.Middleware;
using MedApp.Application.Attachments.Commands.UploadAttachment;
using MedApp.Application.Attachments.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Patients.Common;
using MedApp.Domain.Attachments;
using MedApp.Domain.Patients;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Infrastructure.Attachments;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.AspNetCore.Http;
using Moq;
using static MedApp.UnitTests.Common.Constants.AttachmentsTestConstants;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using Status = MedApp.Domain.Pathways.PatientPathways.PatientPathwayStatus;

namespace MedApp.UnitTests.Application.Attachments;

/// <summary>
/// The fixes from PR #85's review that aren't about hostile files: the upload's second check of what it's attached to
/// (finding 6), staged files left by a crash (7), the 503 for a failing store (9), anonymisation's audit entries (10)
/// and what the retention review counts as a change (3).
/// </summary>
[TestFixture]
public sealed class ReviewFixesTests : AttachmentHandlerTestBase
{
    private UploadAttachmentHandler Handler => Fixture.Create<UploadAttachmentHandler>();

    private static UploadAttachmentCommand Upload(Guid? patientId = null, Guid? pathwayId = null, Guid? stageId = null) =>
        new(patientId, pathwayId, stageId, null, FileName, Pdf.Length, Stream(Pdf), Staff, false);

    private static Patient PatientWith(Action<Patient> change)
    {
        var patient = PatientsTestConstants.CreateValidPatient();
        change(patient);
        return patient;
    }

    [Test]
    public async Task Upload_RecordRestrictedWhileTheFileArrived_IsRefusedAndNothingIsStored()
    {
        var patient = PatientsTestConstants.CreateValidPatient();
        Patients.SetupSequence(p => p.GetByIdAsync(patient.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(patient)
            .ReturnsAsync(PatientWith(p => p.RestrictedAtUtc = Now));

        var act = () => Handler.Handle(Upload(patientId: patient.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(PatientRules.RestrictedMessage);
        Store.Blobs.Should().BeEmpty();
        Added.Should().BeEmpty();
    }

    [Test]
    public async Task Upload_RecordAnonymisedWhileTheFileArrived_IsRefused()
    {
        var patient = PatientsTestConstants.CreateValidPatient();
        Patients.SetupSequence(p => p.GetByIdAsync(patient.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(patient)
            .ReturnsAsync(PatientWith(p => p.AnonymisedAtUtc = Now));

        var act = () => Handler.Handle(Upload(patientId: patient.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(PatientRules.AnonymisedMessage);
    }

    [Test]
    public async Task Upload_PatientDeletedWhileTheFileArrived_IsANotFound()
    {
        var patient = PatientsTestConstants.CreateValidPatient();
        Patients.SetupSequence(p => p.GetByIdAsync(patient.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(patient)
            .ReturnsAsync((Patient?)null);

        var act = () => Handler.Handle(Upload(patientId: patient.Id), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        Store.Blobs.Should().BeEmpty();
    }

    [Test]
    public async Task Upload_StageFinishedWhileTheFileArrived_IsRefused()
    {
        var open = CreatePathway(Status.InProgress);
        var finished = CreatePathway(Status.InProgress, finishedStages: 1, patient: open.Patient);
        var finishedCopy = new PatientPathway
        {
            Id = open.Id, Title = open.Title, PatientId = open.PatientId, Patient = open.Patient, Status = finished.Status,
            Stages = finished.Stages.Select((s, i) => { var copy = CreateStage(open.Id, s.StageOrder, s.StageDefinitionId, s.Name); return copy; }).ToList(),
        };
        // The same stage IDs, the first one now finished.
        for (var i = 0; i < finishedCopy.Stages.Count; i++)
            finishedCopy.Stages[i] = WithId(finishedCopy.Stages[i], open.Stages[i].Id, finished: i == 0);
        var stage = open.StageAt(1);
        Attachments.Setup(a => a.GetStagePathwayIdAsync(stage.Id, It.IsAny<CancellationToken>())).ReturnsAsync(open.Id);
        Repository.SetupSequence(r => r.GetByIdAsync(open.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(open)
            .ReturnsAsync(finishedCopy);

        var act = () => Handler.Handle(Upload(stageId: stage.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*finished and locked*");
        Store.Blobs.Should().BeEmpty();
    }

    private static PatientPathwayStage WithId(PatientPathwayStage stage, Guid id, bool finished)
    {
        var copy = new PatientPathwayStage
        {
            Id = id, PatientPathwayId = stage.PatientPathwayId, StageDefinitionId = stage.StageDefinitionId,
            StageOrder = stage.StageOrder, Name = stage.Name,
        };
        if (finished)
            Finish(copy);
        return copy;
    }

    [Test]
    public async Task Upload_TargetLooksUpTwice_BeforeAndAfterTheFileArrives()
    {
        var patient = GivenPatient();

        await Handler.Handle(Upload(patientId: patient.Id), CancellationToken.None);

        Patients.Verify(p => p.GetByIdAsync(patient.Id, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Test]
    public void Staging_OnLinux_LeavesNoNameBehindEvenWhileOpen()
    {
        using var stream = new TempFileUploadStaging().Create();
        var path = ((FileStream)stream).Name;
        stream.WriteByte(7);
        stream.Position = 0;

        stream.ReadByte().Should().Be(7);
        File.Exists(path).Should().Be(OperatingSystem.IsWindows(), "on Linux the name is deleted as soon as it's opened");
    }

    [Test]
    public void Sweep_DeletesOldStagedFilesOnly()
    {
        var folder = Directory.CreateTempSubdirectory("medapp-sweep-test-").FullName;
        try
        {
            var old = Path.Combine(folder, $"{TempFileUploadStaging.Prefix}old.tmp");
            var young = Path.Combine(folder, $"{TempFileUploadStaging.Prefix}young.tmp");
            var other = Path.Combine(folder, "someone-else.tmp");
            foreach (var file in new[] { old, young, other })
                File.WriteAllText(file, "synthetic");
            File.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddHours(-2));

            var swept = StaleUploadSweeper.Sweep(folder, DateTime.UtcNow, TimeSpan.FromHours(1));

            swept.Should().Be(1);
            File.Exists(old).Should().BeFalse();
            File.Exists(young).Should().BeTrue();
            File.Exists(other).Should().BeTrue();
            StaleUploadSweeper.Sweep(folder, DateTime.UtcNow, TimeSpan.Zero).Should().Be(1);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Test]
    public void Sweep_MissingFolder_SweepsNothing()
    {
        StaleUploadSweeper.Sweep(Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}"), DateTime.UtcNow, TimeSpan.Zero)
            .Should().Be(0);
    }

    [Test]
    public async Task Sweeper_StartsAndStops()
    {
        var sweeper = new StaleUploadSweeper(Microsoft.Extensions.Logging.Abstractions.NullLogger<StaleUploadSweeper>.Instance);

        await sweeper.StartAsync(CancellationToken.None);
        await sweeper.StopAsync(CancellationToken.None);
    }

    [Test]
    public async Task ExceptionHandling_StorageUnavailable_IsA503()
    {
        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        var middleware = new ExceptionHandling(_ => Task.FromException(new StorageUnavailableException(new IOException("Synthetic"))));

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        context.Response.Body.Position = 0;
        JsonDocument.Parse(context.Response.Body).RootElement.GetProperty("title").GetString().Should().StartWith("File storage isn't available");
    }

    [Test]
    public void Anonymise_AuditsEachFileItDeletes_WithItsNewName()
    {
        var pathway = CreatePathway(Status.Completed, finishedStages: 3);
        var record = PrivacyTestConstants.CreateRecord(null, pathway);
        var onStage = new Attachment
        {
            Id = Guid.NewGuid(), PatientId = record.Patient.Id, PathwayId = pathway.Id, StageId = pathway.StageAt(1).Id,
            FileName = "synthetic-canary.pdf", ContentType = "application/pdf",
        };
        var onPatient = new Attachment { Id = Guid.NewGuid(), PatientId = record.Patient.Id, FileName = "a.png", ContentType = "image/png" };
        var alreadyDeleted = new Attachment { Id = Guid.NewGuid(), PatientId = record.Patient.Id, FileName = "b.pdf", ContentType = "application/pdf", DeletedAtUtc = Now };
        var withFiles = new PatientRecord
        {
            Patient = record.Patient, Pathways = record.Pathways, PathwayHistory = record.PathwayHistory,
            Activity = record.Activity, Appointments = record.Appointments, AppointmentHistory = record.AppointmentHistory, PatientMessages = record.PatientMessages,
            Notifications = record.Notifications, Attachments = [onStage, onPatient, alreadyDeleted],
            NoteVersions = [],
            NoteDeletionRequests = [],
            Referrals = [],
            ReferralHistory = [],
            Letters = [],
            Specimens = [],
            MdtItems = []
        };

        var entries = PatientAnonymiser.Apply(withFiles, Now, ActivityTestConstants.Actor);

        entries.Should().HaveCount(2).And.AllSatisfy(e =>
        {
            e.Action.Should().Be(PatientPathwayAuditAction.AttachmentDeleted);
            e.UserId.Should().Be(ActivityTestConstants.Actor.UserId!.Value);
            e.Details.Should().NotContain("canary");
        });
        entries[0].PatientPathwayId.Should().Be(pathway.Id);
        entries[0].StageId.Should().Be(pathway.StageAt(1).Id);
        entries[0].Details.Should().Contain("\"fileName\":\"Anonymised file.pdf\"");
        entries[1].PatientPathwayId.Should().Be(PatientPathwayAuditEntry.NoPathway);
        entries[1].AtUtc.Should().BeAfter(entries[0].AtUtc);
    }

    [Test]
    public void Anonymise_NoFiles_NoEntries()
    {
        PatientAnonymiser.Apply(PrivacyTestConstants.CreateRecord(null), Now).Should().BeEmpty();
    }

    [Test]
    public void Retention_OnlyReadsAreLeftOut()
    {
        RetentionSchedule.ReadOnlyAuditActions.Should().BeEquivalentTo([PatientPathwayAuditAction.AttachmentDownloaded]);
    }
}
