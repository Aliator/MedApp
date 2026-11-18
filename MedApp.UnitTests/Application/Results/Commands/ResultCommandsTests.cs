using FluentAssertions;
using FluentValidation;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Pathways.Repositories;
using MedApp.Application.Patients.Common;
using MedApp.Application.Patients.Repositories;
using MedApp.Application.Results.Commands.MarkResultEnteredInError;
using MedApp.Application.Results.Commands.MarkResultSeen;
using MedApp.Application.Results.Commands.ReassignResult;
using MedApp.Application.Results.Commands.RecordExternalResult;
using MedApp.Application.Results.Commands.SignOffResult;
using MedApp.Application.Results.Common;
using MedApp.Domain.Attachments;
using MedApp.Domain.Notifications;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Results;
using MedApp.Domain.Specimens;
using MedApp.UnitTests.Common.Constants;
using Moq;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using static MedApp.UnitTests.Common.Constants.ResultsTestConstants;

namespace MedApp.UnitTests.Application.Results.Commands;

[TestFixture]
public sealed class ResultCommandsTests : ResultTestBase
{
    private MarkResultSeenHandler SeenHandler => new(Repository.Object, Identity.Object, Holders.Object, Clock);

    private SignOffResultHandler SignOffHandler =>
        new(Repository.Object, Identity.Object, Holders.Object, Notifications.Object, Clock);

    private ReassignResultHandler ReassignHandler =>
        new(Repository.Object, Identity.Object, Holders.Object, Notifications.Object, Clock);

    private MarkResultEnteredInErrorHandler ErrorHandler => new(Repository.Object, Identity.Object, Holders.Object, Clock);

    [Test]
    public async Task MarkSeen_Awaiting_MarksItSeenAndAudits()
    {
        var result = Given(CreateResult());

        var response = await SeenHandler.Handle(new MarkResultSeenCommand(result.Id, 1, Clinician), Cts.Token);

        result.Status.Should().Be(ResultStatus.Seen);
        result.SeenByUserId.Should().Be(Clinician.UserId);
        result.SeenAtUtc.Should().Be(Now);
        result.Version.Should().Be(2);
        SavedAudit.Should().ContainSingle().Which.Action.Should().Be(PatientPathwayAuditAction.ResultSeen);
        Tokens.Should().Equal(Cts.Token);
        response.Status.Should().Be(nameof(ResultStatus.Seen));
        response.CanMarkSeen.Should().BeFalse();
        response.CanSignOff.Should().BeTrue();
    }

    [Test]
    public async Task MarkSeen_StaleVersion_Throws409()
    {
        var result = Given(CreateResult());

        var act = () => SeenHandler.Handle(new MarkResultSeenCommand(result.Id, 7, Clinician), Cts.Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(ResultWorkflow.ChangedMeanwhileMessage);
        SavedAudit.Should().BeEmpty();
    }

    [Test]
    public async Task MarkSeen_AlreadySeen_Throws409()
    {
        var result = Given(CreateResult(status: ResultStatus.Seen));

        var act = () => SeenHandler.Handle(new MarkResultSeenCommand(result.Id, 1, Clinician), Cts.Token);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Test]
    public async Task SignOff_ByTheResponsibleClinician_RecordsTheActionAndTellsNobody()
    {
        var result = Given(CreateResult(status: ResultStatus.Seen));

        await SignOffHandler.Handle(
            new SignOffResultCommand(result.Id, nameof(ResultAction.PatientInformed), $"  {ActionDetail} ", 1, Clinician), Cts.Token);

        result.Status.Should().Be(ResultStatus.SignedOff);
        result.Action.Should().Be(ResultAction.PatientInformed);
        result.ActionDetail.Should().Be(ActionDetail);
        result.SignedOffByUserId.Should().Be(Clinician.UserId);
        var entry = SavedAudit.Should().ContainSingle().Subject;
        entry.Action.Should().Be(PatientPathwayAuditAction.ResultSignedOff);
        entry.Details.Should().Contain("PatientInformed").And.NotContain(ActionDetail);
        Sent.Should().BeEmpty();
    }

    [Test]
    public async Task SignOff_BySomeoneElse_TellsTheResponsibleClinician()
    {
        var result = Given(CreateResult());

        await SignOffHandler.Handle(
            new SignOffResultCommand(result.Id, nameof(ResultAction.NoFurtherAction), null, 1, OtherClinician), Cts.Token);

        var told = Sent.Should().ContainSingle().Subject;
        told.Kind.Should().Be(NotificationKind.ResultSignedOffByOther);
        told.UserId.Should().Be(Clinician.UserId);
        told.SubjectId.Should().Be(result.Id);
        told.Title.Should().Be(ResultNotifications.SignedOffTitle);
        told.Message.Should().Be($"{OtherClinician.Username} signed off a result you're responsible for.");
    }

    [Test]
    public async Task SignOff_AlreadySignedOff_Throws409_AsSignedOffIsFinal()
    {
        var result = Given(CreateResult(status: ResultStatus.SignedOff));

        var act = () => SignOffHandler.Handle(
            new SignOffResultCommand(result.Id, nameof(ResultAction.NoFurtherAction), null, 1, Clinician), Cts.Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*signed off*");
    }

    [Test]
    public async Task SignOff_RestrictedRecord_IsAllowed_AsAResultMustNeverBeStuck()
    {
        var result = Given(CreateResult());
        result.Patient.RestrictedAtUtc = Now.AddDays(-1);

        await SignOffHandler.Handle(
            new SignOffResultCommand(result.Id, nameof(ResultAction.NoFurtherAction), null, 1, Clinician), Cts.Token);

        result.Status.Should().Be(ResultStatus.SignedOff);
    }

    [Test]
    public async Task SignOff_AnonymisedRecord_Throws409()
    {
        var result = Given(CreateResult());
        result.Patient.AnonymisedAtUtc = Now.AddDays(-1);

        var act = () => SignOffHandler.Handle(
            new SignOffResultCommand(result.Id, nameof(ResultAction.NoFurtherAction), null, 1, Clinician), Cts.Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(PatientRules.AnonymisedMessage);
    }

    [Test]
    public void SignOffValidator_OtherWithoutDetail_UnknownAction_AndNoVersion_AreRefused()
    {
        var validator = new SignOffResultValidator();

        validator.Validate(new SignOffResultCommand(Guid.NewGuid(), nameof(ResultAction.Other), " ", 1, Clinician))
            .Errors.Select(e => e.ErrorMessage).Should().Contain(ResultRules.OtherDetailMessage);
        validator.Validate(new SignOffResultCommand(Guid.NewGuid(), "Abnormal", null, 0, Clinician))
            .Errors.Select(e => e.PropertyName).Should().BeEquivalentTo(["Action", "ExpectedVersion"]);
        validator.Validate(new SignOffResultCommand(Guid.NewGuid(), nameof(ResultAction.Other), new string('x', 1001), 1, Clinician))
            .Errors.Should().ContainSingle(e => e.PropertyName == "Detail");
    }

    [Test]
    public async Task Reassign_ToAnotherClinician_ChangesTheResponsibleAndTellsThem()
    {
        var result = Given(CreateResult());

        await ReassignHandler.Handle(new ReassignResultCommand(result.Id, OtherClinician.UserId, 1, Clinician), Cts.Token);

        result.ResponsibleUserId.Should().Be(OtherClinician.UserId);
        var entry = SavedAudit.Should().ContainSingle().Subject;
        entry.Action.Should().Be(PatientPathwayAuditAction.ResultReassigned);
        entry.Details.Should().Contain(Staff.Username).And.Contain(Admin.Username);
        var told = Sent.Should().ContainSingle().Subject;
        told.Kind.Should().Be(NotificationKind.ResultReassigned);
        told.UserId.Should().Be(OtherClinician.UserId);
    }

    [Test]
    public async Task Reassign_TakingItOver_TellsNobody()
    {
        var result = Given(CreateResult());

        await ReassignHandler.Handle(new ReassignResultCommand(result.Id, OtherClinician.UserId, 1, OtherClinician), Cts.Token);

        result.ResponsibleUserId.Should().Be(OtherClinician.UserId);
        Sent.Should().BeEmpty();
    }

    [Test]
    public async Task Reassign_ToSomeoneWhoCantSignOff_Throws400()
    {
        var result = Given(CreateResult());

        var act = () => ReassignHandler.Handle(new ReassignResultCommand(result.Id, OtherUserId, 1, Clinician), Cts.Token);

        (await act.Should().ThrowAsync<ValidationException>())
            .Which.Errors.Should().ContainSingle(e => e.ErrorMessage == ResultWorkflow.ResponsibleNotClinicianMessage);
        SavedAudit.Should().BeEmpty();
    }

    [Test]
    public async Task Reassign_ToTheSameClinician_Throws400()
    {
        var result = Given(CreateResult());

        var act = () => ReassignHandler.Handle(new ReassignResultCommand(result.Id, Clinician.UserId, 1, Clinician), Cts.Token);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task EnteredInError_ByTheReporterWithin24Hours_ClosesItDeletesTheFileAndSendsTheSpecimenBack()
    {
        var result = Given(CreateResult());
        var report = ResultsTestConstants.ReportOf(result);
        Given(result, report);
        var specimen = result.Specimen!;
        var specimenVersion = specimen.Version;

        await ErrorHandler.Handle(
            new MarkResultEnteredInErrorCommand(result.Id, $" {ErrorDetail} ", 1, SpecimensTestConstants.LabStaff), Cts.Token);

        result.Status.Should().Be(ResultStatus.EnteredInError);
        result.ErrorDetail.Should().Be(ErrorDetail);
        report.IsDeleted.Should().BeTrue();
        report.DeletedByUserId.Should().Be(SpecimensTestConstants.LabStaff.UserId);
        specimen.Status.Should().Be(SpecimenStatus.Received);
        specimen.ReportAttachmentId.Should().BeNull();
        specimen.Version.Should().Be(specimenVersion + 1);
        SavedAudit.Select(e => e.Action).Should().Equal(
            PatientPathwayAuditAction.ResultEnteredInError, PatientPathwayAuditAction.AttachmentDeleted);
        SavedAudit.Should().AllSatisfy(e => e.Details.Should().NotContain(ErrorDetail));
        Tokens.Should().Equal(Cts.Token);
    }

    [Test]
    public async Task EnteredInError_ByTheReporterAfter24Hours_Throws403()
    {
        var result = Given(CreateResult(reportedAtUtc: Now.AddHours(-25)));

        var act = () => ErrorHandler.Handle(
            new MarkResultEnteredInErrorCommand(result.Id, ErrorDetail, 1, SpecimensTestConstants.LabStaff), Cts.Token);

        await act.Should().ThrowAsync<ForbiddenException>().WithMessage(ResultWorkflow.EnteredInErrorNotAllowedMessage);
        result.Status.Should().Be(ResultStatus.AwaitingSignOff);
    }

    [Test]
    public async Task EnteredInError_ByCorrectRecords_AnyTime_EvenSignedOff()
    {
        var result = Given(CreateResult(status: ResultStatus.SignedOff, reportedAtUtc: Now.AddDays(-30)));

        await ErrorHandler.Handle(
            new MarkResultEnteredInErrorCommand(result.Id, ErrorDetail, 1, SpecimensTestConstants.Corrector), Cts.Token);

        result.Status.Should().Be(ResultStatus.EnteredInError);
    }

    [Test]
    public async Task EnteredInError_AlreadyEnteredInError_Throws409()
    {
        var result = Given(CreateResult(status: ResultStatus.EnteredInError));

        var act = () => ErrorHandler.Handle(
            new MarkResultEnteredInErrorCommand(result.Id, ErrorDetail, 1, SpecimensTestConstants.Corrector), Cts.Token);

        await act.Should().ThrowAsync<ConflictException>();
    }
}

[TestFixture]
public sealed class RecordExternalResultTests : ResultTestBase
{
    private Mock<IPatientRepository> _patients = null!;
    private Mock<IPatientPathwayRepository> _pathways = null!;
    private PatientPathway _pathway = null!;

    [SetUp]
    public void SetUpRecords()
    {
        _pathway = CreatePathway(PatientPathwayStatus.InProgress);
        _patients = new Mock<IPatientRepository>();
        _patients.Setup(p => p.GetByIdAsync(_pathway.PatientId, It.IsAny<CancellationToken>())).ReturnsAsync(_pathway.Patient);
        _pathways = new Mock<IPatientPathwayRepository>();
        _pathways.Setup(p => p.GetByIdAsync(_pathway.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_pathway);
    }

    private RecordExternalResultHandler Handler =>
        new(Repository.Object, _patients.Object, _pathways.Object, Identity.Object, Holders.Object, Notifications.Object, Clock);

    private Attachment GivenFile(Guid? pathwayId, Guid? referralId = null)
    {
        var file = new Attachment
        {
            Id = Guid.NewGuid(),
            PatientId = _pathway.PatientId,
            PathwayId = pathwayId,
            ReferralId = referralId,
            FileName = "synthetic-scan.pdf",
            ContentType = "application/pdf",
            UploadedByUserId = Staff.UserId,
            UploadedAtUtc = Now.AddDays(-1),
        };
        Repository.Setup(r => r.GetReportAsync(file.Id, It.IsAny<CancellationToken>())).ReturnsAsync(file);
        return file;
    }

    private RecordExternalResultCommand Command(Attachment file, Guid? pathwayId = null, Guid? responsible = null) =>
        new(file.Id, $" {ExternalTitle} ", pathwayId, responsible ?? OtherClinician.UserId, Clinician);

    [Test]
    public async Task Handle_AFileOnAPathway_RecordsAnExternalResultAndTellsTheClinician()
    {
        var file = GivenFile(_pathway.Id);

        var response = await Handler.Handle(Command(file), Cts.Token);

        var result = Added.Should().ContainSingle().Subject;
        result.Source.Should().Be(ResultSource.ExternalReport);
        result.Status.Should().Be(ResultStatus.AwaitingSignOff);
        result.PathwayId.Should().Be(_pathway.Id);
        result.Title.Should().Be(ExternalTitle);
        result.ReportAttachmentId.Should().Be(file.Id);
        result.ResponsibleUserId.Should().Be(OtherClinician.UserId);
        result.ReportedByUserId.Should().Be(Clinician.UserId);
        var entry = SavedAudit.Should().ContainSingle().Subject;
        entry.Action.Should().Be(PatientPathwayAuditAction.ResultRecorded);
        entry.PatientPathwayId.Should().Be(_pathway.Id);
        entry.Details.Should().NotContain(ExternalTitle);
        var told = Sent.Should().ContainSingle().Subject;
        told.Kind.Should().Be(NotificationKind.ResultReady);
        told.Message.Should().Be($"{Clinician.Username} recorded a result from another service.");
        response.Source.Should().Be(nameof(ResultSource.ExternalReport));
    }

    [Test]
    public async Task Handle_AFileOnThePatient_WithNoPathway_IsAboutThePatient()
    {
        var file = GivenFile(null);

        await Handler.Handle(Command(file), Cts.Token);

        Added.Single().PathwayId.Should().BeNull();
        SavedAudit.Single().PatientPathwayId.Should().Be(PatientPathwayAuditEntry.NoPathway);
    }

    [Test]
    public async Task Handle_AnotherPathwayThanTheFiles_Throws400()
    {
        var file = GivenFile(_pathway.Id);

        var act = () => Handler.Handle(Command(file, pathwayId: Guid.NewGuid()), Cts.Token);

        await act.Should().ThrowAsync<ValidationException>();
        Added.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_AReferralsLetter_Throws400()
    {
        var file = GivenFile(null, referralId: Guid.NewGuid());

        var act = () => Handler.Handle(Command(file), Cts.Token);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task Handle_AlreadyAResult_Throws409()
    {
        var file = GivenFile(_pathway.Id);
        Repository.Setup(r => r.IsReportAsync(file.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var act = () => Handler.Handle(Command(file), Cts.Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(RecordExternalResultHandler.AlreadyResultMessage);
    }

    [Test]
    public async Task Handle_ResponsibleCantSignOff_Throws400()
    {
        var file = GivenFile(_pathway.Id);

        var act = () => Handler.Handle(Command(file, responsible: OtherUserId), Cts.Token);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task Handle_RestrictedRecord_Throws409_AsItsNewWork()
    {
        var file = GivenFile(_pathway.Id);
        _pathway.Patient.RestrictedAtUtc = Now.AddDays(-1);

        var act = () => Handler.Handle(Command(file), Cts.Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(ResultWorkflow.RestrictedRecordMessage);
    }

    [Test]
    public async Task Handle_DeletedFile_Throws404()
    {
        var file = GivenFile(_pathway.Id);
        file.DeletedAtUtc = Now;

        var act = () => Handler.Handle(Command(file), Cts.Token);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public void Validator_NoTitleOrFile_IsRefused()
    {
        new RecordExternalResultValidator()
            .Validate(new RecordExternalResultCommand(Guid.Empty, " ", null, Guid.Empty, Clinician))
            .Errors.Select(e => e.PropertyName).Should().BeEquivalentTo(["AttachmentId", "Title", "ResponsibleUserId"]);
    }
}
