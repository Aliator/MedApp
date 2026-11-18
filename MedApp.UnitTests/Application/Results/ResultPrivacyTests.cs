using AutoFixture;
using FluentAssertions;
using MedApp.Application.Activity.Common;
using MedApp.Application.Attachments.Commands.DeleteAttachment;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Pathways.PatientPathways.Commands.DeletePatientPathway;
using MedApp.Application.Pathways.PatientPathways.Commands.FinishPathwayStage;
using MedApp.Application.Patients.Commands.AnonymisePatient;
using MedApp.Application.Patients.Common;
using MedApp.Application.Patients.Repositories;
using MedApp.Application.Results.Common;
using MedApp.Domain.Activity;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Results;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using static MedApp.UnitTests.Common.Constants.ResultsTestConstants;
using Status = MedApp.Domain.Pathways.PatientPathways.PatientPathwayStatus;

namespace MedApp.UnitTests.Application.Results;

/// <summary>Results in the rest of the record: the stage gates, deleting, files, the export and anonymisation.</summary>
[TestFixture]
public sealed class ResultGateAndDeletionTests : AttachmentHandlerTestBase
{
    [Test]
    public async Task FinishLastStage_WithAResultAwaitingSignOff_Throws409_AndSavesNothing()
    {
        var pathway = GivenPathway(CreatePathway(Status.InProgress, finishedStages: 2));
        Results.Setup(r => r.GetAwaitingForPathwayAsync(pathway.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([CreateResult(pathway)]);

        var act = () => Fixture.Create<FinishPathwayStageHandler>()
            .Handle(new FinishPathwayStageCommand(pathway.Id, pathway.StageAt(3).Id, Staff), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("The pathway's last stage*sign-off*");
        VerifyNotSaved();
    }

    [Test]
    public async Task FinishAStageThatWaitsForResults_WithOneAwaiting_Throws409()
    {
        var pathway = GivenPathway(CreatePathway(Status.InProgress));
        pathway.StageAt(1).WaitsForResults = true;
        Results.Setup(r => r.GetAwaitingForPathwayAsync(pathway.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([CreateResult(pathway, ResultStatus.Seen)]);

        var act = () => Fixture.Create<FinishPathwayStageHandler>()
            .Handle(new FinishPathwayStageCommand(pathway.Id, pathway.StageAt(1).Id, Staff), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("This stage waits for results*");
        VerifyNotSaved();
    }

    [Test]
    public async Task FinishAnEarlierStage_ThatDoesntWait_DoesntAsk()
    {
        var pathway = GivenPathway(CreatePathway(Status.InProgress));

        await Fixture.Create<FinishPathwayStageHandler>()
            .Handle(new FinishPathwayStageCommand(pathway.Id, pathway.StageAt(1).Id, Staff), CancellationToken.None);

        Results.Verify(r => r.GetAwaitingForPathwayAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifySavedOnce();
    }

    [Test]
    public async Task DeletePathway_WithResults_Throws409()
    {
        var pathway = GivenPathway(CreatePathway(Status.Cancelled));
        Results.Setup(r => r.CountForPathwayAsync(pathway.Id, It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var act = () => Fixture.Create<DeletePatientPathwayHandler>()
            .Handle(new DeletePatientPathwayCommand(pathway.Id, Admin), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("This pathway has 1 result, so it can't be deleted.");
    }

    [Test]
    public async Task DeleteAttachment_AResultsReport_Throws409_EvenForAdmins()
    {
        GivenPatient(AttachmentsTestConstants.PatientId);
        var attachment = GivenAttachment(AttachmentsTestConstants.CreateAttachment(uploadedBy: Staff.UserId));
        Results.Setup(r => r.IsReportAsync(attachment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var act = () => Fixture.Create<DeleteAttachmentHandler>()
            .Handle(new DeleteAttachmentCommand(attachment.Id, Admin, CanCorrectRecords: true), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(ResultWorkflow.ReportFileMessage);
        attachment.IsDeleted.Should().BeFalse();
    }
}

[TestFixture]
public sealed class ResultRecordTests
{
    private static PatientRecord RecordWith(params Result[] results)
    {
        var pathway = CreatePathway(Status.Completed);
        var record = PrivacyTestConstants.CreateRecord(pathway.Patient, pathway);
        return record with { Results = results.Select(r => { r.Patient = record.Patient; return r; }).ToList() };
    }

    [Test]
    public void Anonymiser_ReplacesFreeText_KeepsStatusesActionsAndDates()
    {
        var result = CreateResult(status: ResultStatus.SignedOff);
        result.Action = ResultAction.Other;
        result.ActionDetail = ActionDetail;
        result.ErrorDetail = ErrorDetail;
        var reportedAt = result.ReportedAtUtc;

        PatientAnonymiser.Apply(RecordWith(result), Now);

        result.Title.Should().Be(PatientAnonymiser.RemovedText);
        result.ActionDetail.Should().Be(PatientAnonymiser.RemovedText);
        result.ErrorDetail.Should().Be(PatientAnonymiser.RemovedText);
        result.Status.Should().Be(ResultStatus.SignedOff);
        result.Action.Should().Be(ResultAction.Other);
        result.ReportedAtUtc.Should().Be(reportedAt);
    }

    [Test]
    public void Export_ListsResultsWithTheirFreeText_AndStaffByUsernameOnly()
    {
        var result = CreateResult(status: ResultStatus.SignedOff);
        result.SignedOffByUserId = Clinician.UserId;
        result.SignedOffAtUtc = Now;
        result.Action = ResultAction.PatientInformed;
        result.ActionDetail = ActionDetail;
        var record = RecordWith(result);

        PatientExportMapper.UserIds(record).Should().Contain([Clinician.UserId, SpecimensTestConstants.LabStaff.UserId]);
        var export = PatientExportMapper.ToResponse(record, Usernames, Now, "exporter");

        export.Results.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            result.Id,
            Source = nameof(ResultSource.Specimen),
            result.Title,
            ReportedBy = OtherUsername,
            ResponsibleClinician = Staff.Username,
            Status = nameof(ResultStatus.SignedOff),
            SignedOffBy = Staff.Username,
            Action = nameof(ResultAction.PatientInformed),
            ActionDetail,
        });
    }

    [TestCase(ResultStatus.AwaitingSignOff)]
    [TestCase(ResultStatus.Seen)]
    public async Task Anonymise_WithAResultAwaitingSignOff_Throws409(ResultStatus status)
    {
        var record = RecordWith(CreateResult(status: status));
        record = record with { Specimens = [] };
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

        await act.Should().ThrowAsync<ConflictException>().WithMessage("This patient has 1 result awaiting sign-off*");
        repository.Verify(r => r.SaveAsync(It.IsAny<ActivityEntry>(), It.IsAny<IReadOnlyCollection<PatientPathwayAuditEntry>>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
