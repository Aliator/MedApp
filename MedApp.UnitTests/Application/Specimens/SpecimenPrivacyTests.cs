using AutoFixture;
using FluentAssertions;
using MedApp.Application.Activity.Common;
using MedApp.Application.Attachments.Commands.DeleteAttachment;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.PatientPathways.Commands.DeletePatientPathway;
using MedApp.Application.Pathways.PatientPathways.Commands.FinishPathwayStage;
using MedApp.Application.Patients.Commands.AnonymisePatient;
using MedApp.Application.Patients.Common;
using MedApp.Application.Patients.Repositories;
using MedApp.Application.Specimens.Common;
using MedApp.Domain.Activity;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Specimens;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using static MedApp.UnitTests.Common.Constants.SpecimensTestConstants;
using Status = MedApp.Domain.Pathways.PatientPathways.PatientPathwayStatus;

namespace MedApp.UnitTests.Application.Specimens;

/// <summary>Specimens in the rest of the record: the stage gate, deleting, files, the export and anonymisation.</summary>
[TestFixture]
public sealed class SpecimenGateAndDeletionTests : AttachmentHandlerTestBase
{
    [Test]
    public async Task FinishLastStage_WithAnOpenSpecimen_Throws409NamingIt_AndSavesNothing()
    {
        var pathway = GivenPathway(CreatePathway(Status.InProgress, finishedStages: 2));
        Specimens.Setup(s => s.GetOpenForPathwayAsync(pathway.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([CreateSpecimen(pathway, SpecimenStatus.Received)]);

        var act = () => Fixture.Create<FinishPathwayStageHandler>()
            .Handle(new FinishPathwayStageCommand(pathway.Id, pathway.StageAt(3).Id, Staff), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage($"*{Number}*");
        VerifyNotSaved();
    }

    [Test]
    public async Task FinishAnEarlierStage_WithAnOpenSpecimen_DoesntAsk()
    {
        var pathway = GivenPathway(CreatePathway(Status.InProgress));

        await Fixture.Create<FinishPathwayStageHandler>()
            .Handle(new FinishPathwayStageCommand(pathway.Id, pathway.StageAt(1).Id, Staff), CancellationToken.None);

        Specimens.Verify(s => s.GetOpenForPathwayAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifySavedOnce();
    }

    [Test]
    public async Task FinishLastStage_AllSpecimensBack_Finishes()
    {
        var pathway = GivenPathway(CreatePathway(Status.InProgress, finishedStages: 2));

        await Fixture.Create<FinishPathwayStageHandler>()
            .Handle(new FinishPathwayStageCommand(pathway.Id, pathway.StageAt(3).Id, Staff), CancellationToken.None);

        VerifySavedOnce();
    }

    [Test]
    public async Task DeletePathway_WithSpecimens_Throws409()
    {
        var pathway = GivenPathway(CreatePathway(Status.Cancelled));
        Specimens.Setup(s => s.CountForPathwayAsync(pathway.Id, It.IsAny<CancellationToken>())).ReturnsAsync(2);

        var act = () => Fixture.Create<DeletePatientPathwayHandler>()
            .Handle(new DeletePatientPathwayCommand(pathway.Id, Admin), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("This pathway has 2 specimens, so it can't be deleted.");
    }

    [Test]
    public async Task DeleteAttachment_ASpecimensReport_Throws409_EvenForAdmins()
    {
        GivenPatient(AttachmentsTestConstants.PatientId);
        var attachment = GivenAttachment(AttachmentsTestConstants.CreateAttachment(uploadedBy: Staff.UserId));
        Specimens.Setup(s => s.IsReportAsync(attachment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var act = () => Fixture.Create<DeleteAttachmentHandler>()
            .Handle(new DeleteAttachmentCommand(attachment.Id, Admin, CanCorrectRecords: true), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(SpecimenWorkflow.ReportedFileMessage);
        attachment.IsDeleted.Should().BeFalse();
    }
}

[TestFixture]
public sealed class SpecimenRecordTests
{
    private static PatientRecord RecordWith(params Specimen[] specimens)
    {
        var pathway = CreatePathway(Status.Completed);
        var record = PrivacyTestConstants.CreateRecord(pathway.Patient, pathway);
        return record with { Specimens = specimens.Select(s => { s.Patient = record.Patient; return s; }).ToList() };
    }

    [Test]
    public void Anonymiser_ReplacesFreeTextAndReferences_KeepsNumbersStatusesAndDates()
    {
        var specimen = CreateSpecimen(status: SpecimenStatus.RejectedByLab);
        specimen.CourierReference = "BATCH-1";
        specimen.LabReference = "LAB-1";
        var record = RecordWith(specimen);

        PatientAnonymiser.Apply(record, Now);

        specimen.Site.Should().Be(PatientAnonymiser.RemovedText);
        specimen.RequestNotes.Should().Be(PatientAnonymiser.RemovedText);
        specimen.ClosedDetail.Should().Be(PatientAnonymiser.RemovedText);
        specimen.CourierReference.Should().BeNull();
        specimen.LabReference.Should().BeNull();
        specimen.Number.Should().Be(Number);
        specimen.Status.Should().Be(SpecimenStatus.RejectedByLab);
        specimen.RejectionReason.Should().Be(SpecimenRejectionReason.Leaked);
    }

    [Test]
    public void Export_ListsSpecimensWithStaffByUsernameOnly()
    {
        var specimen = CreateSpecimen(status: SpecimenStatus.Reported);
        var record = RecordWith(specimen);

        var usernames = PatientExportMapper.UserIds(record).ToHashSet();
        var export = PatientExportMapper.ToResponse(record, Usernames, Now, "exporter");

        usernames.Should().Contain([Staff.UserId, OtherUserId]);
        var exported = export.Specimens.Should().ContainSingle().Subject;
        exported.Should().BeEquivalentTo(new
        {
            Number,
            SpecimenTypeName = BiopsyName,
            LaboratoryName = InHouseLabName,
            Site = specimen.Site,
            RequestNotes = specimen.RequestNotes,
            RequestedBy = Staff.Username,
            TakenBy = Staff.Username,
            Status = "Reported",
            ReportAttachmentId = specimen.ReportAttachmentId,
            ClosedBy = (string?)null,
        });
    }

    [TestCase(SpecimenStatus.Taken)]
    [TestCase(SpecimenStatus.Sent)]
    [TestCase(SpecimenStatus.Received)]
    public async Task Anonymise_WithASpecimenStillWithTheLab_Throws409(SpecimenStatus status)
    {
        var record = RecordWith(CreateSpecimen(status: status));
        var repository = new Mock<IPatientRecordRepository>();
        repository.Setup(r => r.GetAsync(record.Patient.Id, true, It.IsAny<CancellationToken>())).ReturnsAsync(record);
        var fixture = AutoFixtureFactory.Create();
        fixture.Inject(repository.Object);
        fixture.Inject<IActivityLog>(new RecordingActivityLog());
        fixture.Inject<TimeProvider>(new FixedTimeProvider(Now));

        var act = () => fixture.Create<AnonymisePatientHandler>().Handle(
            new AnonymisePatientCommand(record.Patient.Id, "retentionPeriodEnded", null,
                $"{record.Patient.FirstName} {record.Patient.LastName}"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("This patient has 1 specimen still with the lab*");
        repository.Verify(r => r.SaveAsync(It.IsAny<ActivityEntry>(), It.IsAny<IReadOnlyCollection<PatientPathwayAuditEntry>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public void PathwayMapper_CanRecordSpecimen_FollowsThePermissionAndTheStage()
    {
        var pathway = CreatePathway(Status.InProgress);
        var usernames = new Dictionary<Guid, string>();

        var nurse = PathwayMapper.ToResponse(pathway, usernames, Now, viewerPermissions: Nurse.Permissions);
        var lab = PathwayMapper.ToResponse(pathway, usernames, Now, viewerPermissions: LabStaff.Permissions);

        nurse.Stages.Select(s => s.CanRecordSpecimen).Should().Equal(true, false, false);
        lab.Stages.Should().OnlyContain(s => !s.CanRecordSpecimen);
    }
}
