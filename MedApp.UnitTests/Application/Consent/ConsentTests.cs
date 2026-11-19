using FluentAssertions;
using FluentValidation;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Consent.Commands.AttachConsentScan;
using MedApp.Application.Consent.Commands.MarkConsentEnteredInError;
using MedApp.Application.Consent.Commands.RecordConsent;
using MedApp.Application.Consent.Commands.WithdrawConsent;
using MedApp.Application.Consent.Common;
using MedApp.Application.Consent.Queries.GetPathwayConsentRecords;
using MedApp.Application.Consent.Repositories;
using MedApp.Application.Consent.Setup;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.Repositories;
using MedApp.Domain.Activity;
using MedApp.Domain.Attachments;
using MedApp.Domain.Consent;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;

namespace MedApp.UnitTests.Application.Consent;

/// <summary>
/// Consent records: the rules, the gate, the commands, the pathway's list and the forms. MedApp records consent; these
/// check it never does more than record what the clinician states.
/// </summary>
[TestFixture]
public sealed class ConsentTests
{
    private Mock<IConsentRepository> _repository = null!;
    private Mock<IPatientPathwayRepository> _pathways = null!;
    private Mock<IIdentityReadService> _identity = null!;
    private readonly FixedTimeProvider _clock = new(Now);
    private readonly List<ConsentRecord> _added = [];
    private readonly List<PatientPathwayAuditEntry> _saved = [];
    private readonly List<CancellationToken> _tokens = [];
    private CancellationTokenSource _cts = null!;
    private TeamWallsTestBoundary _walls = null!;

    [SetUp]
    public void SetUp()
    {
        _added.Clear();
        _saved.Clear();
        _tokens.Clear();
        _cts = new CancellationTokenSource();
        _walls = new TeamWallsTestBoundary();
        _repository = new Mock<IConsentRepository>();
        _repository.Setup(r => r.GetFormAsync(ConsentTestConstants.Form().Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ConsentTestConstants.Form());
        _repository
            .Setup(r => r.AddAsync(It.IsAny<ConsentRecord>(), It.IsAny<IEnumerable<PatientPathwayAuditEntry>>(), It.IsAny<CancellationToken>()))
            .Callback((ConsentRecord record, IEnumerable<PatientPathwayAuditEntry> audit, CancellationToken ct) =>
            {
                _added.Add(record);
                _saved.AddRange(audit);
                _tokens.Add(ct);
            })
            .Returns(Task.CompletedTask);
        _repository
            .Setup(r => r.SaveAsync(It.IsAny<IEnumerable<PatientPathwayAuditEntry>>(), It.IsAny<CancellationToken>()))
            .Callback((IEnumerable<PatientPathwayAuditEntry> audit, CancellationToken ct) =>
            {
                _saved.AddRange(audit);
                _tokens.Add(ct);
            })
            .Returns(Task.CompletedTask);
        _pathways = new Mock<IPatientPathwayRepository>();
        _identity = new Mock<IIdentityReadService>();
        _identity.Setup(i => i.GetUsernamesByIdAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [Staff.UserId] = Staff.Username, [Admin.UserId] = Admin.Username });
    }

    [TearDown]
    public void TearDown() => _cts.Dispose();

    private PatientPathway GivenPathway(PatientPathwayStatus status = PatientPathwayStatus.InProgress)
    {
        var pathway = CreatePathway(status);
        _pathways.Setup(p => p.GetByIdAsync(pathway.Id, It.IsAny<CancellationToken>())).ReturnsAsync(pathway);
        return pathway;
    }

    private ConsentRecord GivenRecord(ConsentRecord record)
    {
        _repository.Setup(r => r.GetForUpdateAsync(record.Id, It.IsAny<CancellationToken>())).ReturnsAsync(record);
        return record;
    }

    private Attachment GivenFileOn(PatientPathway pathway, bool deleted = false)
    {
        var file = new Attachment
        {
            Id = Guid.NewGuid(), PatientId = pathway.PatientId, PathwayId = pathway.Id, FileName = "signed.pdf",
            ContentType = "application/pdf", DeletedAtUtc = deleted ? Now : null,
        };
        _repository.Setup(r => r.GetAttachmentForUpdateAsync(file.Id, It.IsAny<CancellationToken>())).ReturnsAsync(file);
        return file;
    }

    private RecordConsentHandler RecordHandler() => new(_pathways.Object, _walls.Boundary, _repository.Object, _identity.Object, _clock);

    private static RecordConsentCommand Record(PatientPathway pathway, Guid? scan = null, DateOnly? signedOn = null, string signedBy = "Patient") =>
        new(pathway.Id, pathway.StageAt(1).Id, ConsentTestConstants.Form().Id, signedOn ?? Today, signedBy, scan, ConsentTestConstants.Recorder);

    // The rules.

    [Test]
    public void WhyCantRecord_OpenPathwayStageToDo_IsNull()
    {
        var pathway = CreatePathway(PatientPathwayStatus.InProgress);
        ConsentWorkflow.WhyCantRecord(pathway, pathway.StageAt(2)).Should().BeNull("consent is often taken ahead of its stage");
    }

    [Test]
    public void WhyCantRecord_RestrictedRecord_SaysAskAnAdmin()
    {
        var pathway = CreatePathway(PatientPathwayStatus.InProgress);
        pathway.Patient.RestrictedAtUtc = Now;
        ConsentWorkflow.WhyCantRecord(pathway, pathway.StageAt(1)).Should().Be(ConsentWorkflow.RestrictedMessage);
    }

    [TestCase(PatientPathwayStatus.Blocked)]
    [TestCase(PatientPathwayStatus.Cancelled)]
    [TestCase(PatientPathwayStatus.Completed)]
    public void WhyCantRecord_PathwayNotOpen_Refuses(PatientPathwayStatus status)
    {
        var pathway = CreatePathway(status);
        ConsentWorkflow.WhyCantRecord(pathway, pathway.StageAt(1)).Should().NotBeNull();
    }

    [Test]
    public void WhyCantRecord_FinishedOrSkippedStage_Refuses()
    {
        var pathway = CreatePathway(PatientPathwayStatus.InProgress);
        pathway.StageAt(1).IsCompleted = true;
        pathway.StageAt(2).SkippedAtUtc = Now;
        ConsentWorkflow.WhyCantRecord(pathway, pathway.StageAt(1)).Should().Contain("finished");
        ConsentWorkflow.WhyCantRecord(pathway, pathway.StageAt(2)).Should().Contain("skipped");
    }

    [Test]
    public void WhySignedOnInvalid_FutureOrOverAYearBack_Refuses()
    {
        ConsentWorkflow.WhySignedOnInvalid(Today, Today).Should().BeNull();
        ConsentWorkflow.WhySignedOnInvalid(Today.AddDays(-365), Today).Should().BeNull();
        ConsentWorkflow.WhySignedOnInvalid(Today.AddDays(1), Today).Should().Contain("future");
        ConsentWorkflow.WhySignedOnInvalid(Today.AddDays(-366), Today).Should().Contain("year");
    }

    [Test]
    public void MayMarkEnteredInError_RecorderWithin24Hours_OrCorrectRecords()
    {
        var record = ConsentTestConstants.CreateRecord(recordedAtUtc: Now.AddHours(-1));
        ConsentWorkflow.MayMarkEnteredInError(record, ConsentTestConstants.Recorder, Now).Should().BeTrue();
        ConsentWorkflow.MayMarkEnteredInError(record, ConsentTestConstants.Recorder, Now.AddDays(1)).Should().BeFalse();
        ConsentWorkflow.MayMarkEnteredInError(record, ConsentTestConstants.Corrector, Now.AddDays(30)).Should().BeTrue();
        ConsentWorkflow.MayMarkEnteredInError(record, new PathwayActor(OtherUserId, OtherUsername, ConsentTestConstants.Recorder.Permissions), Now)
            .Should().BeFalse("only whoever recorded it");
    }

    [Test]
    public void IsOnPathway_LiveFileOnThePathwayOrItsStage_OnlyThose()
    {
        var pathway = CreatePathway(PatientPathwayStatus.InProgress);
        var onStage = new Attachment { Id = Guid.NewGuid(), PatientId = pathway.PatientId, StageId = pathway.StageAt(2).Id };
        var elsewhere = new Attachment { Id = Guid.NewGuid(), PatientId = pathway.PatientId, PathwayId = Guid.NewGuid() };
        var deleted = new Attachment { Id = Guid.NewGuid(), PatientId = pathway.PatientId, PathwayId = pathway.Id, DeletedAtUtc = Now };

        ConsentWorkflow.IsOnPathway(onStage, pathway).Should().BeTrue();
        ConsentWorkflow.IsOnPathway(elsewhere, pathway).Should().BeFalse();
        ConsentWorkflow.IsOnPathway(deleted, pathway).Should().BeFalse();
    }

    [Test]
    public void WithdrawnAfterStageFinished_OnlyWhenWithdrawnAfterTheFinish()
    {
        var record = ConsentTestConstants.CreateRecord(status: ConsentStatus.Withdrawn);
        var stage = record.Pathway.StageAt(1);
        stage.CompletedAtUtc = Now.AddHours(-2);
        ConsentWorkflow.WithdrawnAfterStageFinished(record, stage).Should().BeTrue();
        stage.CompletedAtUtc = Now;
        ConsentWorkflow.WithdrawnAfterStageFinished(record, stage).Should().BeFalse();
    }

    // The gate.

    [Test]
    public void WhyConsentMissing_StageWithoutTheFlag_IsNull()
    {
        var pathway = CreatePathway(PatientPathwayStatus.InProgress);
        StageGates.WhyConsentMissing(pathway.StageAt(1), []).Should().BeNull();
    }

    [Test]
    public void WhyConsentMissing_NeedsConsent_OnlyAValidRecordForTheStageMeetsIt()
    {
        var pathway = CreatePathway(PatientPathwayStatus.InProgress);
        var stage = pathway.StageAt(1);
        stage.NeedsConsent = true;

        StageGates.WhyConsentMissing(stage, []).Should().Be(StageGates.ConsentMissingMessage);
        StageGates.WhyConsentMissing(stage, [ConsentTestConstants.CreateRecord(pathway, ConsentStatus.Withdrawn)])
            .Should().Contain("withdrawn");
        StageGates.WhyConsentMissing(stage, [ConsentTestConstants.CreateRecord(pathway, ConsentStatus.EnteredInError)])
            .Should().Be(StageGates.ConsentMissingMessage);
        StageGates.WhyConsentMissing(stage, [ConsentTestConstants.CreateRecord(pathway)]).Should().BeNull();
    }

    // Recording.

    [Test]
    public async Task Record_Valid_AddsItWithItsAuditEntry_HoldingNoFreeText()
    {
        var pathway = GivenPathway();
        var scan = GivenFileOn(pathway);

        var response = await RecordHandler().Handle(Record(pathway, scan.Id, signedBy: "PersonWithParentalResponsibility"), _cts.Token);

        var record = _added.Should().ContainSingle().Subject;
        record.Should().BeEquivalentTo(new
        {
            Status = ConsentStatus.Valid, SignedBy = ConsentSignedBy.PersonWithParentalResponsibility, ScanAttachmentId = (Guid?)scan.Id,
            FormName = ConsentTestConstants.FormName, FormVersion = ConsentTestConstants.FormVersion, Version = 1, StageId = pathway.StageAt(1).Id,
        });
        var audit = _saved.Should().ContainSingle().Subject;
        audit.Action.Should().Be(PatientPathwayAuditAction.ConsentRecorded);
        audit.Details.Should().Contain("\"signedBy\":\"PersonWithParentalResponsibility\"").And.Contain(scan.Id.ToString());
        response.Status.Should().Be("Valid");
        response.RecordedByUsername.Should().Be(Staff.Username);
        response.CanWithdraw.Should().BeTrue();
        response.CanAttachScan.Should().BeFalse("it has its scan");
        _tokens.Should().OnlyContain(t => t == _cts.Token);
    }

    [Test]
    public async Task Record_RestrictedRecord_IsAConflict()
    {
        var pathway = GivenPathway();
        pathway.Patient.RestrictedAtUtc = Now;

        var act = () => RecordHandler().Handle(Record(pathway), _cts.Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(ConsentWorkflow.RestrictedMessage);
        _added.Should().BeEmpty();
    }

    [Test]
    public async Task Record_ArchivedForm_IsA400()
    {
        var pathway = GivenPathway();
        _repository.Setup(r => r.GetFormAsync(ConsentTestConstants.Form().Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ConsentTestConstants.Form(archived: true));

        var act = () => RecordHandler().Handle(Record(pathway), _cts.Token);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task Record_SignedInTheFuture_IsA400()
    {
        var pathway = GivenPathway();
        var act = () => RecordHandler().Handle(Record(pathway, signedOn: Today.AddDays(1)), _cts.Token);
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task Record_ScanNotOnThePathway_OrAlreadyAScan_IsA400()
    {
        var pathway = GivenPathway();
        var deleted = GivenFileOn(pathway, deleted: true);
        var used = GivenFileOn(pathway);
        _repository.Setup(r => r.IsScanAsync(used.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await FluentActions.Invoking(() => RecordHandler().Handle(Record(pathway, deleted.Id), _cts.Token))
            .Should().ThrowAsync<ValidationException>().WithMessage($"*{ConsentWorkflow.ScanNotFoundMessage}*");
        await FluentActions.Invoking(() => RecordHandler().Handle(Record(pathway, used.Id), _cts.Token))
            .Should().ThrowAsync<ValidationException>().WithMessage($"*{ConsentWorkflow.ScanInUseMessage}*");
    }

    [Test]
    public void RecordValidator_UnknownSignedBy_IsRefused()
    {
        var pathway = CreatePathway(PatientPathwayStatus.InProgress);
        new RecordConsentValidator().Validate(Record(pathway, signedBy: "Neighbour")).IsValid.Should().BeFalse();
        new RecordConsentValidator().Validate(Record(pathway)).IsValid.Should().BeTrue();
    }

    // Withdrawing.

    [Test]
    public async Task Withdraw_Valid_RecordsTheDateAndDetail_OnlyOnTheRecord()
    {
        var record = GivenRecord(ConsentTestConstants.CreateRecord());
        var handler = new WithdrawConsentHandler(_repository.Object, _walls.Boundary, _identity.Object, _clock);

        var response = await handler.Handle(new WithdrawConsentCommand(record.Id, Today, " Synthetic: changed their mind. ", 1, ConsentTestConstants.Recorder), _cts.Token);

        record.Status.Should().Be(ConsentStatus.Withdrawn);
        record.StatusDetail.Should().Be("Synthetic: changed their mind.");
        record.Version.Should().Be(2);
        var audit = _saved.Should().ContainSingle().Subject;
        audit.Action.Should().Be(PatientPathwayAuditAction.ConsentWithdrawn);
        audit.Details.Should().NotContain("changed their mind");
        response.Status.Should().Be("Withdrawn");
        response.CanWithdraw.Should().BeFalse();
    }

    [Test]
    public async Task Withdraw_WhileRestricted_IsAllowed()
    {
        var record = GivenRecord(ConsentTestConstants.CreateRecord());
        record.Patient.RestrictedAtUtc = Now;
        var handler = new WithdrawConsentHandler(_repository.Object, _walls.Boundary, _identity.Object, _clock);

        await handler.Handle(new WithdrawConsentCommand(record.Id, Today, "Synthetic.", 1, ConsentTestConstants.Recorder), _cts.Token);

        record.Status.Should().Be(ConsentStatus.Withdrawn);
    }

    [Test]
    public async Task Withdraw_StaleVersion_OrNotValid_IsAConflict()
    {
        var stale = GivenRecord(ConsentTestConstants.CreateRecord());
        var withdrawn = GivenRecord(ConsentTestConstants.CreateRecord(status: ConsentStatus.Withdrawn));
        var handler = new WithdrawConsentHandler(_repository.Object, _walls.Boundary, _identity.Object, _clock);

        await FluentActions.Invoking(() => handler.Handle(new WithdrawConsentCommand(stale.Id, Today, "x", 9, ConsentTestConstants.Recorder), _cts.Token))
            .Should().ThrowAsync<ConflictException>().WithMessage(ConsentWorkflow.ChangedMeanwhileMessage);
        await FluentActions.Invoking(() => handler.Handle(new WithdrawConsentCommand(withdrawn.Id, Today, "x", 1, ConsentTestConstants.Recorder), _cts.Token))
            .Should().ThrowAsync<ConflictException>();
    }

    [Test]
    public async Task Withdraw_BeforeItWasSigned_IsA400()
    {
        var record = GivenRecord(ConsentTestConstants.CreateRecord());
        var handler = new WithdrawConsentHandler(_repository.Object, _walls.Boundary, _identity.Object, _clock);

        await FluentActions.Invoking(() => handler.Handle(new WithdrawConsentCommand(record.Id, Today.AddDays(-1), "x", 1, ConsentTestConstants.Recorder), _cts.Token))
            .Should().ThrowAsync<ValidationException>();
    }

    // Entered in error.

    [Test]
    public async Task EnteredInError_ByACorrector_SoftDeletesTheScan_WithAnAuditEntry()
    {
        var pathway = CreatePathway(PatientPathwayStatus.InProgress);
        var scan = GivenFileOn(pathway);
        var record = GivenRecord(ConsentTestConstants.CreateRecord(pathway, scanAttachmentId: scan.Id, recordedAtUtc: Now.AddDays(-3)));
        var handler = new MarkConsentEnteredInErrorHandler(_repository.Object, _walls.Boundary, _identity.Object, _clock);

        await handler.Handle(new MarkConsentEnteredInErrorCommand(record.Id, "Synthetic: wrong patient.", 1, ConsentTestConstants.Corrector), _cts.Token);

        record.Status.Should().Be(ConsentStatus.EnteredInError);
        scan.IsDeleted.Should().BeTrue();
        scan.DeletedByUserId.Should().Be(Admin.UserId);
        _saved.Select(a => a.Action).Should().Equal(PatientPathwayAuditAction.ConsentEnteredInError, PatientPathwayAuditAction.AttachmentDeleted);
    }

    [Test]
    public async Task EnteredInError_RecorderAfter24Hours_IsForbidden()
    {
        var record = GivenRecord(ConsentTestConstants.CreateRecord(recordedAtUtc: Now.AddDays(-2)));
        var handler = new MarkConsentEnteredInErrorHandler(_repository.Object, _walls.Boundary, _identity.Object, _clock);

        await FluentActions.Invoking(() => handler.Handle(new MarkConsentEnteredInErrorCommand(record.Id, "x", 1, ConsentTestConstants.Recorder), _cts.Token))
            .Should().ThrowAsync<ForbiddenException>();
    }

    [Test]
    public async Task EnteredInError_Anonymised_IsAConflict()
    {
        var record = GivenRecord(ConsentTestConstants.CreateRecord());
        record.Patient.AnonymisedAtUtc = Now;
        var handler = new MarkConsentEnteredInErrorHandler(_repository.Object, _walls.Boundary, _identity.Object, _clock);

        await FluentActions.Invoking(() => handler.Handle(new MarkConsentEnteredInErrorCommand(record.Id, "x", 1, ConsentTestConstants.Corrector), _cts.Token))
            .Should().ThrowAsync<ConflictException>();
    }

    // The scan, later.

    [Test]
    public async Task AttachScan_RecordWithoutOne_TakesTheFile()
    {
        var pathway = CreatePathway(PatientPathwayStatus.InProgress);
        var scan = GivenFileOn(pathway);
        var record = GivenRecord(ConsentTestConstants.CreateRecord(pathway));
        var handler = new AttachConsentScanHandler(_repository.Object, _walls.Boundary, _identity.Object, _clock);

        var response = await handler.Handle(new AttachConsentScanCommand(record.Id, scan.Id, 1, ConsentTestConstants.Recorder), _cts.Token);

        record.ScanAttachmentId.Should().Be(scan.Id);
        record.Version.Should().Be(2);
        response.ScanAttachmentId.Should().Be(scan.Id);
        _tokens.Should().ContainSingle().Which.Should().Be(_cts.Token);
    }

    [Test]
    public async Task AttachScan_AlreadyHasOne_IsAConflict()
    {
        var record = GivenRecord(ConsentTestConstants.CreateRecord(scanAttachmentId: Guid.NewGuid()));
        var handler = new AttachConsentScanHandler(_repository.Object, _walls.Boundary, _identity.Object, _clock);

        await FluentActions.Invoking(() => handler.Handle(new AttachConsentScanCommand(record.Id, Guid.NewGuid(), 1, ConsentTestConstants.Recorder), _cts.Token))
            .Should().ThrowAsync<ConflictException>();
    }

    // The pathway's list.

    [Test]
    public async Task GetPathwayConsentRecords_RecordsPathwayViewed_AndGivesTheViewersFlags()
    {
        var pathway = GivenPathway();
        var record = ConsentTestConstants.CreateRecord(pathway);
        _repository.Setup(r => r.GetForPathwayAsync(pathway.Id, It.IsAny<CancellationToken>())).ReturnsAsync([record]);
        var activity = new RecordingActivityLog();
        var handler = new GetPathwayConsentRecordsHandler(_pathways.Object, _walls.Boundary, _repository.Object, _identity.Object, activity, _clock);

        var list = await handler.Handle(new GetPathwayConsentRecordsQuery(pathway.Id, ConsentTestConstants.Corrector), _cts.Token);

        list.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            StageName = pathway.StageAt(1).Name, CanWithdraw = false, CanMarkEnteredInError = true, CanAttachScan = false,
        });
        var view = activity.Views.Should().ContainSingle().Subject;
        view.Action.Should().Be(ActivityAction.PathwayViewed);
        view.PatientIds.Should().Equal(pathway.PatientId);
        view.PathwayId.Should().Be(pathway.Id);
    }

    [Test]
    public async Task GetPathwayConsentRecords_UnknownPathway_IsNotFound()
    {
        var handler = new GetPathwayConsentRecordsHandler(_pathways.Object, _walls.Boundary, _repository.Object, _identity.Object, new RecordingActivityLog(), _clock);
        await FluentActions.Invoking(() => handler.Handle(new GetPathwayConsentRecordsQuery(Guid.NewGuid(), ConsentTestConstants.Recorder), _cts.Token))
            .Should().ThrowAsync<NotFoundException>();
    }

    // Consent forms.

    [Test]
    public async Task Forms_Create_SameNameAndVersion_IsAConflict()
    {
        _repository.Setup(r => r.FormExistsAsync(ConsentTestConstants.FormName, "3", null, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var handlers = new ConsentFormHandlers(_repository.Object, _clock);

        await FluentActions.Invoking(() => handlers.Handle(new CreateConsentFormCommand($" {ConsentTestConstants.FormName} ", "3"), _cts.Token))
            .Should().ThrowAsync<ConflictException>();
    }

    [Test]
    public async Task Forms_Create_TrimsAndAdds()
    {
        ConsentForm? added = null;
        _repository.Setup(r => r.AddFormAsync(It.IsAny<ConsentForm>(), It.IsAny<CancellationToken>()))
            .Callback((ConsentForm f, CancellationToken _) => added = f).Returns(Task.CompletedTask);
        var handlers = new ConsentFormHandlers(_repository.Object, _clock);

        var response = await handlers.Handle(new CreateConsentFormCommand(" Synthetic form ", " 2 "), _cts.Token);

        added.Should().BeEquivalentTo(new { Name = "Synthetic form", FormVersion = "2", CreatedAt = Now });
        response.CanBeDeleted.Should().BeTrue();
    }

    [Test]
    public async Task Forms_DeleteOneInUse_IsAConflict_ArchiveIsAllowed()
    {
        var form = ConsentTestConstants.Form();
        _repository.Setup(r => r.GetFormsInUseAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<Guid> { form.Id });
        var handlers = new ConsentFormHandlers(_repository.Object, _clock);

        await FluentActions.Invoking(() => handlers.Handle(new DeleteConsentFormCommand(form.Id), _cts.Token))
            .Should().ThrowAsync<ConflictException>();
        var archived = await handlers.Handle(new SetConsentFormArchivedCommand(form.Id, true), _cts.Token);
        archived.IsArchived.Should().BeTrue();
        archived.CanBeDeleted.Should().BeFalse();
    }

    [Test]
    public void FormValidators_NeedANameAndVersion()
    {
        new CreateConsentFormValidator().Validate(new CreateConsentFormCommand("", "1")).IsValid.Should().BeFalse();
        new CreateConsentFormValidator().Validate(new CreateConsentFormCommand("Form", "")).IsValid.Should().BeFalse();
        new UpdateConsentFormValidator().Validate(new UpdateConsentFormCommand(Guid.NewGuid(), "Form", new string('1', 51))).IsValid.Should().BeFalse();
        new UpdateConsentFormValidator().Validate(new UpdateConsentFormCommand(Guid.NewGuid(), "Form", "1")).IsValid.Should().BeTrue();
    }

    [Test]
    public void CommandValidators_NeedADetailAndAVersion()
    {
        new WithdrawConsentValidator().Validate(new WithdrawConsentCommand(Guid.NewGuid(), Today, " ", 1, ConsentTestConstants.Recorder)).IsValid.Should().BeFalse();
        new MarkConsentEnteredInErrorValidator().Validate(new MarkConsentEnteredInErrorCommand(Guid.NewGuid(), "x", 0, ConsentTestConstants.Recorder)).IsValid.Should().BeFalse();
        new AttachConsentScanValidator().Validate(new AttachConsentScanCommand(Guid.NewGuid(), Guid.Empty, 1, ConsentTestConstants.Recorder)).IsValid.Should().BeFalse();
    }
}
