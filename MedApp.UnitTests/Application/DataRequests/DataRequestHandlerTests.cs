using FluentAssertions;
using FluentValidation;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.DataRequests.Commands;
using MedApp.Application.DataRequests.Common;
using MedApp.Application.DataRequests.Jobs;
using MedApp.Application.DataRequests.Queries;
using MedApp.Application.DataRequests.Repositories;
using MedApp.Application.Patients.Common;
using MedApp.Application.Patients.Repositories;
using MedApp.Domain.Activity;
using MedApp.Domain.DataRequests;
using MedApp.Domain.Notifications;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using static MedApp.UnitTests.Common.Constants.DataRequestTestConstants;

namespace MedApp.UnitTests.Application.DataRequests;

/// <summary>The data request commands, queries and reminder job, against a mocked repository.</summary>
[TestFixture]
public sealed class DataRequestHandlerTests
{
    private Mock<IDataRequestRepository> _repository = null!;
    private Mock<IPatientRepository> _patients = null!;
    private Mock<IPatientRecordRepository> _records = null!;
    private Mock<IIdentityReadService> _identity = null!;
    private RecordingActivityLog _activity = null!;
    private readonly FixedTimeProvider _clock = new(PathwaysTestConstants.Now);
    private readonly List<ActivityEntry> _saved = [];
    private CancellationTokenSource _cts = null!;

    [SetUp]
    public void SetUp()
    {
        _saved.Clear();
        _cts = new CancellationTokenSource();
        _repository = new Mock<IDataRequestRepository>();
        _repository.Setup(r => r.SaveAsync(It.IsAny<ActivityEntry>(), It.IsAny<CancellationToken>()))
            .Callback((ActivityEntry entry, CancellationToken _) => _saved.Add(entry))
            .Returns(Task.CompletedTask);
        _repository.Setup(r => r.AddAsync(It.IsAny<DataRequest>(), It.IsAny<ActivityEntry>(), It.IsAny<CancellationToken>()))
            .Callback((DataRequest _, ActivityEntry entry, CancellationToken _) => _saved.Add(entry))
            .Returns(Task.CompletedTask);
        _patients = new Mock<IPatientRepository>();
        _records = new Mock<IPatientRecordRepository>();
        _identity = new Mock<IIdentityReadService>();
        _identity.Setup(i => i.GetUsernamesByIdAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string>());
        _activity = new RecordingActivityLog();
    }

    [TearDown]
    public void TearDown() => _cts.Dispose();

    private DataRequest Given(DataRequest request)
    {
        _repository.Setup(r => r.GetForUpdateAsync(request.Id, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        _repository.Setup(r => r.GetAsync(request.Id, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        return request;
    }

    [Test]
    public async Task Create_OneMonthDue_RecordedWithoutTheRequestersDetails()
    {
        var patient = PatientsTestConstants.CreateValidPatient();
        _patients.Setup(p => p.GetByIdAsync(patient.Id, It.IsAny<CancellationToken>())).ReturnsAsync(patient);
        var handler = new CreateDataRequestHandler(_patients.Object, _repository.Object, _activity, _clock);

        var response = await handler.Handle(
            new CreateDataRequestCommand(patient.Id, Today, false, $"  {RequesterName} ", Relationship, Admin), _cts.Token);

        response.DueOn.Should().Be(Today.AddMonths(1));
        response.RequesterName.Should().Be(RequesterName);
        response.Version.Should().Be(1);
        var entry = _saved.Single();
        entry.Action.Should().Be(ActivityAction.DataRequestChanged);
        entry.PatientId.Should().Be(patient.Id);
        entry.Details.Should().Contain("Recorded").And.NotContain(RequesterName).And.NotContain(Relationship);
        _repository.Verify(r => r.AddAsync(It.IsAny<DataRequest>(), It.IsAny<ActivityEntry>(), _cts.Token));
    }

    [Test]
    public async Task Create_AnAnonymisedRecord_IsAConflict_AFutureDateIsInvalid()
    {
        var anonymised = PrivacyTestConstants.CreateAnonymisedPatient();
        var patient = PatientsTestConstants.CreateValidPatient();
        _patients.Setup(p => p.GetByIdAsync(anonymised.Id, It.IsAny<CancellationToken>())).ReturnsAsync(anonymised);
        _patients.Setup(p => p.GetByIdAsync(patient.Id, It.IsAny<CancellationToken>())).ReturnsAsync(patient);
        var handler = new CreateDataRequestHandler(_patients.Object, _repository.Object, _activity, _clock);

        var anonymisedAct = () => handler.Handle(new CreateDataRequestCommand(anonymised.Id, Today, true, null, null, Admin), _cts.Token);
        await anonymisedAct.Should().ThrowAsync<ConflictException>();

        var futureAct = () => handler.Handle(new CreateDataRequestCommand(patient.Id, Today.AddDays(1), true, null, null, Admin), _cts.Token);
        await futureAct.Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public void CreateValidator_SomeoneOnTheirBehalf_NeedsAName()
    {
        var validator = new CreateDataRequestValidator();
        validator.Validate(new CreateDataRequestCommand(Guid.NewGuid(), Today, false, null, null, Admin)).IsValid.Should().BeFalse();
        validator.Validate(new CreateDataRequestCommand(Guid.NewGuid(), Today, true, null, null, Admin)).IsValid.Should().BeTrue();
    }

    [Test]
    public async Task IdentityCheck_RecordsWhoAndHow_AndBumpsTheVersion()
    {
        var request = Given(Open());
        var handler = new RecordIdentityCheckHandler(_repository.Object, _activity, _clock);

        var response = await handler.Handle(new RecordIdentityCheckCommand(request.Id, Today, "Documents", null, 1, Admin), _cts.Token);

        response.IdentityCheckMethod.Should().Be("Documents");
        response.IdentityCheckedByUsername.Should().Be(Admin.Username);
        response.Version.Should().Be(2);
        _saved.Single().Details.Should().Contain("IdentityChecked");
    }

    [Test]
    public async Task Extend_MovesTheDueDate_OnlyWithinTheFirstMonth()
    {
        var request = Given(Open());
        var handler = new ExtendDataRequestHandler(_repository.Object, _activity, _clock);

        var response = await handler.Handle(new ExtendDataRequestCommand(request.Id, 2, "Synthetic complexity.", 1, Admin), _cts.Token);
        response.DueOn.Should().Be(Today.AddMonths(3));

        var late = Given(Open());
        late.Version = 1;
        var lateHandler = new ExtendDataRequestHandler(_repository.Object, _activity, new FixedTimeProvider(PathwaysTestConstants.Now.AddMonths(2)));
        var act = () => lateHandler.Handle(new ExtendDataRequestCommand(late.Id, 1, "Too late.", 1, Admin), _cts.Token);
        await act.Should().ThrowAsync<ConflictException>().WithMessage("*within the first month*");
    }

    [Test]
    public async Task Sent_NeedsTheIdentityCheckAndTheReview_ThenClosesIt()
    {
        var handler = new CloseDataRequestHandler(_repository.Object, _activity, _clock);
        var notReady = Given(Open());
        var act = () => handler.Handle(new RecordDataRequestSentCommand(notReady.Id, Today, "Post", 1, Admin), _cts.Token);
        await act.Should().ThrowAsync<ConflictException>();

        var ready = Given(Ready());
        var response = await handler.Handle(new RecordDataRequestSentCommand(ready.Id, Today, "SecureEmail", 1, Admin), _cts.Token);
        response.Status.Should().Be("Sent");
        response.SendMethod.Should().Be("SecureEmail");
        response.ClosedByUsername.Should().Be(Admin.Username);
        response.CanManage.Should().BeFalse();

        var again = () => handler.Handle(new WithdrawDataRequestCommand(ready.Id, response.Version, Admin), _cts.Token);
        await again.Should().ThrowAsync<ConflictException>().WithMessage(MedApp.Application.DataRequests.Common.DataRequestRules.NotOpenMessage);
    }

    [Test]
    public async Task Review_OnlyItemsInTheRecord_ReplacesTheLastReview()
    {
        var record = PrivacyTestConstants.CreateRecord(null, PathwaysTestConstants.CreatePathway());
        var noteId = Guid.NewGuid();
        record.Pathways[0].Notes.Add(new MedApp.Domain.Pathways.PatientPathways.PatientPathwayNote
        {
            Id = noteId, PatientPathwayId = record.Pathways[0].Id, Text = "Synthetic note.", AuthorUserId = Admin.UserId,
            CreatedAtUtc = PathwaysTestConstants.Now
        });
        _records.Setup(r => r.GetAsync(record.Patient.Id, false, It.IsAny<CancellationToken>())).ReturnsAsync(record);
        var request = Given(Open(record.Patient));
        request.WithheldItems.Add(new DataRequestWithheldItem { Id = Guid.NewGuid(), Kind = DataRequestItemKind.File, ItemId = Guid.NewGuid(), Reason = "Old." });
        var handler = new RecordDataRequestReviewHandler(_repository.Object, _records.Object, _identity.Object, _activity, _clock);

        var unknown = () => handler.Handle(new RecordDataRequestReviewCommand(
            request.Id, [new WithheldItemInput("Note", Guid.NewGuid(), WithheldReason)], 1, Admin), _cts.Token);
        await unknown.Should().ThrowAsync<ValidationException>();

        var response = await handler.Handle(new RecordDataRequestReviewCommand(
            request.Id, [new WithheldItemInput("Note", noteId, $" {WithheldReason} ")], 1, Admin), _cts.Token);

        response.ReviewedByUsername.Should().Be(Admin.Username);
        response.Withheld.Should().ContainSingle(w => w.Kind == "Note" && w.ItemId == noteId && w.Reason == WithheldReason);
        _saved.Single().Details.Should().Contain("Reviewed").And.NotContain(WithheldReason);
    }

    [Test]
    public async Task Get_SomeoneWithoutManagePrivacy_IsForbidden_ClinicianToo_AdminsRecordAView()
    {
        var request = Given(Open());
        var handler = new GetDataRequestHandler(_repository.Object, _activity, _clock);

        var act = () => handler.Handle(new GetDataRequestQuery(request.Id, Clerk), _cts.Token);
        await act.Should().ThrowAsync<ForbiddenException>();
        var clinician = () => handler.Handle(new GetDataRequestQuery(request.Id, Clinician), _cts.Token);
        await clinician.Should().ThrowAsync<ForbiddenException>();

        await handler.Handle(new GetDataRequestQuery(request.Id, Admin), _cts.Token);
        _activity.SingleViews.Should().ContainSingle(v => v.Action == ActivityAction.PatientViewed && v.PatientId == request.PatientId
                                                          && v.Details.Contains(request.Id.ToString()));
    }

    [Test]
    public async Task EveryChange_WithoutManagePrivacy_IsForbidden_BeforeAnythingIsRead()
    {
        var request = Given(Open());
        var record = PrivacyTestConstants.CreateRecord(null, PathwaysTestConstants.CreatePathway());
        var acts = new Func<Task>[]
        {
            () => new CreateDataRequestHandler(_patients.Object, _repository.Object, _activity, _clock)
                .Handle(new CreateDataRequestCommand(request.PatientId, Today, true, null, null, Clinician), _cts.Token),
            () => new RecordIdentityCheckHandler(_repository.Object, _activity, _clock)
                .Handle(new RecordIdentityCheckCommand(request.Id, Today, "Documents", null, 1, Clinician), _cts.Token),
            () => new ExtendDataRequestHandler(_repository.Object, _activity, _clock)
                .Handle(new ExtendDataRequestCommand(request.Id, 1, "Synthetic.", 1, Clinician), _cts.Token),
            () => new RecordDataRequestReviewHandler(_repository.Object, _records.Object, _identity.Object, _activity, _clock)
                .Handle(new RecordDataRequestReviewCommand(request.Id, [], 1, Clinician), _cts.Token),
            () => new CloseDataRequestHandler(_repository.Object, _activity, _clock)
                .Handle(new WithdrawDataRequestCommand(request.Id, 1, Clinician), _cts.Token),
        };

        foreach (var act in acts)
            await act.Should().ThrowAsync<ForbiddenException>().WithMessage(DataRequestRules.CantHandleMessage);
        _saved.Should().BeEmpty();
        _repository.Verify(r => r.GetForUpdateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Pack_BeforeTheReview_IsAConflict_AndRecordsNothing()
    {
        var request = Given(Open());
        request.IdentityCheckedOn = Today;
        var handler = new GetDataRequestPackHandler(
            _repository.Object, _records.Object, _identity.Object, Mock.Of<MedApp.Application.Attachments.Common.IFileStore>(),
            Mock.Of<MedApp.Application.Letters.Common.ILetterRenderer>(), _activity,
            Microsoft.Extensions.Options.Options.Create(new MedApp.Application.Practices.Common.LettersOptions()),
            Microsoft.Extensions.Options.Options.Create(new RetentionOptions()), _clock);

        var act = () => handler.Handle(new GetDataRequestPackQuery(request.Id, Admin), _cts.Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("Review the record*");
        _activity.Recorded.Should().BeEmpty();
    }

    [Test]
    public async Task RemindersJob_TellsEachManagePrivacyHolderOncePerDueDate()
    {
        var request = Open();
        _repository.Setup(r => r.GetDueSoonUnnotifiedAsync(Today.AddDays(7), DataRequestRemindersJob.BatchSize, It.IsAny<CancellationToken>()))
            .ReturnsAsync([request]);
        IReadOnlyCollection<Notification>? sent = null;
        _repository.Setup(r => r.SaveRemindersAsync(It.IsAny<IReadOnlyCollection<Notification>>(), It.IsAny<CancellationToken>()))
            .Callback((IReadOnlyCollection<Notification> n, CancellationToken _) => sent = n)
            .Returns(Task.CompletedTask);
        var holders = new Mock<IPermissionHolders>();
        holders.Setup(h => h.GetUserIdsAsync(Permission.ManagePrivacy, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Admin.UserId, Guid.NewGuid()]);
        var job = new DataRequestRemindersJob(_repository.Object, holders.Object, NullLogger<DataRequestRemindersJob>.Instance);

        var handled = await job.RunAsync(PathwaysTestConstants.Now, _cts.Token);

        handled.Should().Be(1);
        sent.Should().HaveCount(2).And.OnlyContain(n => n.Kind == NotificationKind.DataRequestDueSoon && n.SubjectId == request.Id);
        request.DueSoonNotifiedFor.Should().Be(request.DueOn);
    }
}
