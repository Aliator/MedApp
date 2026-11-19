using FluentAssertions;
using FluentValidation;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Mdt.Commands.DeferMdtItem;
using MedApp.Application.Mdt.Commands.ListForMdt;
using MedApp.Application.Mdt.Commands.RecordMdtOutcome;
using MedApp.Application.Mdt.Commands.WithdrawMdtItem;
using MedApp.Application.Mdt.Common;
using MedApp.Application.Mdt.Repositories;
using MedApp.Application.Notifications.Common;
using MedApp.Application.Pathways.Repositories;
using MedApp.Application.Patients.Common;
using MedApp.Application.Teams.Repositories;
using MedApp.Domain.Calendar;
using MedApp.Domain.Mdt;
using MedApp.Domain.Notifications;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.MdtTestConstants;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;

namespace MedApp.UnitTests.Application.Mdt.Commands;

/// <summary>Shared mocks: the item repository keeps what's saved, and every read after a save returns the item.</summary>
public abstract class MdtCommandTestBase
{
    protected Mock<IMdtItemRepository> Repository { get; private set; } = null!;
    protected Mock<ITeamRepository> Teams { get; private set; } = null!;
    protected List<PatientPathwayAuditEntry> Audit { get; } = [];
    protected FixedTimeProvider Clock { get; } = new(Now);
    protected CancellationToken Token { get; } = new CancellationTokenSource().Token;
    protected TeamWallsTestBoundary Walls { get; private set; } = null!;

    [SetUp]
    public void SetUpMdtMocks()
    {
        Audit.Clear();
        Walls = new TeamWallsTestBoundary();
        Repository = new Mock<IMdtItemRepository>();
        Repository
            .Setup(r => r.SaveAsync(It.IsAny<PatientPathwayAuditEntry>(), It.IsAny<CancellationToken>()))
            .Callback((PatientPathwayAuditEntry entry, CancellationToken _) => Audit.Add(entry))
            .Returns(Task.CompletedTask);
        Teams = new Mock<ITeamRepository>();
        Teams.Setup(s => s.GetAsync(Team.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Team);
    }

    protected void Given(MdtItem item)
    {
        Repository.Setup(r => r.GetForUpdateAsync(item.Id, It.IsAny<CancellationToken>())).ReturnsAsync(item);
        Repository.Setup(r => r.GetAsync(item.Id, It.IsAny<CancellationToken>())).ReturnsAsync(item);
    }

    protected void Given(Meeting meeting) =>
        Repository.Setup(r => r.GetMeetingAsync(meeting.Id, It.IsAny<CancellationToken>())).ReturnsAsync(meeting);
}

[TestFixture]
public sealed class ListForMdtHandlerTests : MdtCommandTestBase
{
    private Mock<IPatientPathwayRepository> _pathways = null!;
    private PatientPathway _pathway = null!;
    private Meeting _meeting = null!;
    private MdtItem? _added;

    [SetUp]
    public void SetUp()
    {
        _added = null;
        _pathway = CreatePathway(PatientPathwayStatus.InProgress);
        _pathways = new Mock<IPatientPathwayRepository>();
        _pathways.Setup(p => p.GetByIdAsync(_pathway.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_pathway);
        _meeting = CreateMdt(Now.AddDays(2));
        Given(_meeting);
        Repository
            .Setup(r => r.AddAsync(It.IsAny<MdtItem>(), It.IsAny<PatientPathwayAuditEntry>(), It.IsAny<CancellationToken>()))
            .Callback((MdtItem item, PatientPathwayAuditEntry entry, CancellationToken _) =>
            {
                _added = item;
                Audit.Add(entry);
                item.Patient = _pathway.Patient;
                item.Pathway = _pathway;
                item.Meeting = _meeting;
                item.Team = Team;
                Repository.Setup(r => r.GetAsync(item.Id, It.IsAny<CancellationToken>())).ReturnsAsync(item);
            })
            .Returns(Task.CompletedTask);
    }

    private ListForMdtHandler Handler() => new(Repository.Object, _pathways.Object, Walls.Boundary, Teams.Object, Clock);

    private ListForMdtCommand Command(Guid? meetingId = null) => new(_pathway.Id, meetingId ?? _meeting.Id, $"  {Question}  ", Nurse);

    [Test]
    public async Task Handle_OpenPathwayAndUpcomingMdt_ListsItWithTheQuestionAndAuditsWithoutIt()
    {
        var response = await Handler().Handle(Command(), Token);

        _added!.Status.Should().Be(MdtItemStatus.Listed);
        _added.Question.Should().Be(Question);
        _added.TeamId.Should().Be(Team.Id);
        _added.Version.Should().Be(1);
        response.Question.Should().Be(Question);
        response.MeetingId.Should().Be(_meeting.Id);
        Audit.Should().ContainSingle().Which.Action.Should().Be(PatientPathwayAuditAction.MdtListed);
        Audit[0].Details.Should().NotContain(Question).And.Contain("Dermatology");
        Repository.Verify(r => r.AddAsync(It.IsAny<MdtItem>(), It.IsAny<PatientPathwayAuditEntry>(), Token));
    }

    [Test]
    public async Task Handle_RestrictedRecord_Refuses()
    {
        _pathway.Patient.RestrictedAtUtc = Now;

        await FluentActions.Awaiting(() => Handler().Handle(Command(), Token))
            .Should().ThrowAsync<ConflictException>().WithMessage(PatientRules.RestrictedMessage);
    }

    [TestCase(PatientPathwayStatus.Completed)]
    [TestCase(PatientPathwayStatus.Cancelled)]
    [TestCase(PatientPathwayStatus.Archived)]
    public async Task Handle_ClosedPathway_Refuses(PatientPathwayStatus status)
    {
        _pathway.Status = status;

        await FluentActions.Awaiting(() => Handler().Handle(Command(), Token))
            .Should().ThrowAsync<ConflictException>().WithMessage(MdtRules.PathwayClosedMessage);
    }

    [Test]
    public async Task Handle_OrdinaryMeeting_Refuses()
    {
        _meeting.IsMdt = false;

        await FluentActions.Awaiting(() => Handler().Handle(Command(), Token))
            .Should().ThrowAsync<ConflictException>().WithMessage(MdtRules.NotMdtMessage);
    }

    [Test]
    public async Task Handle_MdtEnded_Refuses()
    {
        _meeting.StartUtc = Now.AddHours(-2);
        _meeting.EndUtc = Now.AddHours(-1);

        await FluentActions.Awaiting(() => Handler().Handle(Command(), Token))
            .Should().ThrowAsync<ConflictException>().WithMessage(MdtRules.MeetingEndedMessage);
    }

    [Test]
    public async Task Handle_AlreadyOnThatList_Refuses()
    {
        Repository.Setup(r => r.IsOpenOnMeetingAsync(_pathway.Id, _meeting.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await FluentActions.Awaiting(() => Handler().Handle(Command(), Token))
            .Should().ThrowAsync<ConflictException>().WithMessage(MdtRules.AlreadyListedMessage);
    }

    [Test]
    public async Task Handle_UnknownMeeting_IsNotFound()
    {
        await FluentActions.Awaiting(() => Handler().Handle(Command(Guid.NewGuid()), Token))
            .Should().ThrowAsync<NotFoundException>();
    }
}

[TestFixture]
public sealed class WithdrawMdtItemHandlerTests : MdtCommandTestBase
{
    private WithdrawMdtItemHandler Handler() => new(Repository.Object, Walls.Boundary, Clock);

    [Test]
    public async Task Handle_OpenItem_WithdrawsItAndAudits()
    {
        var item = CreateItem(CreateMdt(Now.AddDays(1)));
        Given(item);

        var response = await Handler().Handle(new WithdrawMdtItemCommand(item.Id, 1, Nurse), Token);

        item.Status.Should().Be(MdtItemStatus.Withdrawn);
        item.WithdrawnByUsername.Should().Be(Nurse.Username);
        item.Version.Should().Be(2);
        response.Status.Should().Be(nameof(MdtItemStatus.Withdrawn));
        Audit.Should().ContainSingle().Which.Action.Should().Be(PatientPathwayAuditAction.MdtWithdrawn);
        Repository.Verify(r => r.SaveAsync(It.IsAny<PatientPathwayAuditEntry>(), Token));
    }

    [Test]
    public async Task Handle_RestrictedRecord_StillWithdraws()
    {
        var item = CreateItem(CreateMdt(Now.AddDays(1)));
        item.Patient.RestrictedAtUtc = Now;
        Given(item);

        await Handler().Handle(new WithdrawMdtItemCommand(item.Id, 1, Nurse), Token);

        item.Status.Should().Be(MdtItemStatus.Withdrawn);
    }

    [Test]
    public async Task Handle_AnonymisedRecord_Refuses()
    {
        var item = CreateItem(CreateMdt(Now.AddDays(1)));
        item.Patient.AnonymisedAtUtc = Now;
        Given(item);

        await FluentActions.Awaiting(() => Handler().Handle(new WithdrawMdtItemCommand(item.Id, 1, Nurse), Token))
            .Should().ThrowAsync<ConflictException>().WithMessage(PatientRules.AnonymisedMessage);
    }

    [Test]
    public async Task Handle_StaleVersion_Refuses()
    {
        var item = CreateItem(CreateMdt(Now.AddDays(1)));
        Given(item);

        await FluentActions.Awaiting(() => Handler().Handle(new WithdrawMdtItemCommand(item.Id, 7, Nurse), Token))
            .Should().ThrowAsync<ConflictException>().WithMessage(MdtRules.ChangedMeanwhileMessage);
    }

    [TestCase(MdtItemStatus.Discussed)]
    [TestCase(MdtItemStatus.Withdrawn)]
    public async Task Handle_Closed_Refuses(MdtItemStatus status)
    {
        var item = CreateItem(CreateMdt(Now.AddDays(1)), status);
        Given(item);

        await FluentActions.Awaiting(() => Handler().Handle(new WithdrawMdtItemCommand(item.Id, 1, Nurse), Token))
            .Should().ThrowAsync<ConflictException>().WithMessage(MdtRules.ClosedMessage);
    }
}

[TestFixture]
public sealed class DeferMdtItemHandlerTests : MdtCommandTestBase
{
    private DeferMdtItemHandler Handler() => new(Repository.Object, Walls.Boundary, Teams.Object, Clock);

    [Test]
    public async Task Handle_ToALaterMdt_DefersItAndAuditsBothMeetings()
    {
        var current = CreateMdt(Now.AddHours(-1));
        var later = CreateMdt(Now.AddDays(7));
        var item = CreateItem(current);
        Given(item);
        Given(later);

        await Handler().Handle(new DeferMdtItemCommand(item.Id, later.Id, 1, Nurse), Token);

        item.Status.Should().Be(MdtItemStatus.Deferred);
        item.MeetingId.Should().Be(later.Id);
        item.Version.Should().Be(2);
        var entry = Audit.Should().ContainSingle().Subject;
        entry.Action.Should().Be(PatientPathwayAuditAction.MdtDeferred);
        entry.Details.Should().Contain(current.Id.ToString()).And.Contain(later.Id.ToString());
    }

    [Test]
    public async Task Handle_MeetingWasCancelled_ListsItAgainAsItWas()
    {
        var later = CreateMdt(Now.AddDays(7));
        var item = CreateItem(null);
        item.MeetingCancelledStartUtc = Now.AddDays(1);
        Given(item);
        Given(later);

        await Handler().Handle(new DeferMdtItemCommand(item.Id, later.Id, 1, Nurse), Token);

        item.Status.Should().Be(MdtItemStatus.Listed);
        item.MeetingId.Should().Be(later.Id);
        item.MeetingCancelledStartUtc.Should().BeNull();
    }

    [Test]
    public async Task Handle_NotLater_Refuses()
    {
        var current = CreateMdt(Now.AddDays(7));
        var earlier = CreateMdt(Now.AddDays(2));
        var item = CreateItem(current);
        Given(item);
        Given(earlier);

        await FluentActions.Awaiting(() => Handler().Handle(new DeferMdtItemCommand(item.Id, earlier.Id, 1, Nurse), Token))
            .Should().ThrowAsync<ConflictException>().WithMessage(MdtRules.NotLaterMessage);
    }

    [Test]
    public async Task Handle_RestrictedRecord_Refuses()
    {
        var item = CreateItem(CreateMdt(Now.AddDays(1)));
        item.Patient.RestrictedAtUtc = Now;
        Given(item);

        await FluentActions.Awaiting(() => Handler().Handle(new DeferMdtItemCommand(item.Id, Guid.NewGuid(), 1, Nurse), Token))
            .Should().ThrowAsync<ConflictException>().WithMessage(PatientRules.RestrictedMessage);
    }
}

[TestFixture]
public sealed class RecordMdtOutcomeHandlerTests : MdtCommandTestBase
{
    private Mock<IIdentityReadService> _identity = null!;
    private Mock<INotificationSender> _notifications = null!;
    private readonly List<Notification> _sent = [];

    [SetUp]
    public void SetUp()
    {
        _sent.Clear();
        _identity = new Mock<IIdentityReadService>();
        _identity
            .Setup(i => i.GetUsernamesByIdAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [OtherUserId] = OtherUsername, [Staff.UserId] = Staff.Username });
        _notifications = new Mock<INotificationSender>();
        _notifications
            .Setup(n => n.SendAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Callback((Notification n, CancellationToken _) => _sent.Add(n))
            .Returns(Task.CompletedTask);
    }

    private RecordMdtOutcomeHandler Handler() => new(Repository.Object, Walls.Boundary, _identity.Object, _notifications.Object, Clock);

    private static RecordMdtOutcomeCommand Command(MdtItem item, params Guid[] present) =>
        new(item.Id, $" {Outcome} ", present.Length == 0 ? [OtherUserId] : present, 1, Nurse with { Permissions = Clinician.Permissions });

    [Test]
    public async Task Handle_MeetingStarted_RecordsTheOutcomeAsWrittenAndWhoWasPresent_AndTellsTheAssignees()
    {
        var item = CreateItem(CreateMdt(Now.AddMinutes(-30)));
        Given(item);

        var response = await Handler().Handle(Command(item), Token);

        item.Status.Should().Be(MdtItemStatus.Discussed);
        item.Outcome.Should().Be(Outcome);
        item.Attendees.Select(a => a.Username).Should().Equal(OtherUsername);
        response.Present.Should().Equal(OtherUsername);
        var entry = Audit.Should().ContainSingle().Subject;
        entry.Action.Should().Be(PatientPathwayAuditAction.MdtOutcomeRecorded);
        entry.Details.Should().NotContain(Outcome).And.NotContain(Question).And.Contain(OtherUsername);
        // The pathway's assignee (Staff) is told, never the recorder; the message holds no outcome or patient.
        var told = _sent.Should().ContainSingle().Subject;
        told.UserId.Should().Be(Staff.UserId);
        told.Kind.Should().Be(NotificationKind.MdtOutcomeRecorded);
        told.Title.Should().Be(MdtNotifications.OutcomeTitle);
        told.Message.Should().NotContain(Outcome).And.NotContain(item.Patient.LastName);
        _notifications.Verify(n => n.SendAsync(It.IsAny<Notification>(), Token));
    }

    [Test]
    public async Task Handle_PathwayUnchanged()
    {
        var item = CreateItem(CreateMdt(Now.AddMinutes(-30)));
        var status = item.Pathway.Status;
        var updated = item.Pathway.LastUpdated;
        Given(item);

        await Handler().Handle(Command(item), Token);

        item.Pathway.Status.Should().Be(status);
        item.Pathway.LastUpdated.Should().Be(updated);
    }

    [Test]
    public async Task Handle_MeetingNotStarted_Refuses()
    {
        var item = CreateItem(CreateMdt(Now.AddHours(1)));
        Given(item);

        await FluentActions.Awaiting(() => Handler().Handle(Command(item), Token))
            .Should().ThrowAsync<ConflictException>().WithMessage(MdtRules.NotStartedMessage);
    }

    [Test]
    public async Task Handle_MeetingCancelled_Refuses()
    {
        var item = CreateItem(null);
        Given(item);

        await FluentActions.Awaiting(() => Handler().Handle(Command(item), Token))
            .Should().ThrowAsync<ConflictException>().WithMessage(MdtRules.NoMeetingMessage);
    }

    [Test]
    public async Task Handle_SomeoneNotInvited_IsABadRequest()
    {
        var item = CreateItem(CreateMdt(Now.AddMinutes(-30)));
        Given(item);

        await FluentActions.Awaiting(() => Handler().Handle(Command(item, Guid.NewGuid()), Token))
            .Should().ThrowAsync<ValidationException>().WithMessage($"*{MdtRules.NotPresentMessage}*");
    }

    [Test]
    public async Task Handle_RestrictedRecord_StillRecords()
    {
        var item = CreateItem(CreateMdt(Now.AddMinutes(-30)));
        item.Patient.RestrictedAtUtc = Now;
        Given(item);

        await Handler().Handle(Command(item), Token);

        item.Status.Should().Be(MdtItemStatus.Discussed);
    }

    [Test]
    public async Task Handle_AnonymisedRecord_Refuses()
    {
        var item = CreateItem(CreateMdt(Now.AddMinutes(-30)));
        item.Patient.AnonymisedAtUtc = Now;
        Given(item);

        await FluentActions.Awaiting(() => Handler().Handle(Command(item), Token))
            .Should().ThrowAsync<ConflictException>().WithMessage(PatientRules.AnonymisedMessage);
    }
}
