using FluentAssertions;
using MedApp.Application.Monitoring.Common;
using MedApp.Domain.Monitoring;
using MedApp.Domain.Notifications;
using MedApp.Domain.Pathways.PatientPathways;
using static MedApp.UnitTests.Common.Constants.MonitoringTestConstants;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using Priority = MedApp.Domain.Pathways.PatientPathways.PatientPathwayPriority;

namespace MedApp.UnitTests.Application.Monitoring.Common;

[TestFixture]
public sealed class MonitoringNotificationsTests
{
    private const string SecretTitle = "Synthetic free text title";

    private PatientPathway _pathway = null!;

    [SetUp]
    public void SetUp() => _pathway = CreatePathway(priority: Priority.High, title: SecretTitle);

    private static MonitoredActivity ByStaff(MonitoringEvent monitoringEvent) =>
        new(monitoringEvent, Staff.Username, Staff.UserId, Staff.Username);

    [Test]
    public void Create_StageFinished_SaysWhoFinishedWhichStageOnWhatKindOfPathway()
    {
        var activity = ByStaff(MonitoringEvent.StageFinished) with { StageDefinitionId = ConsultationDefinitionId, StageName = BloodsStage };

        var notification = MonitoringNotifications.Create(OwnerId, activity, _pathway, Now);

        notification.Should().BeEquivalentTo(new Notification
        {
            Id = notification.Id,
            UserId = OwnerId,
            Kind = NotificationKind.MonitoredStageFinished,
            Title = "Stage finished",
            Message = $"{Staff.Username} finished the {BloodsStage} stage on a High priority {TemplateName} pathway.",
            PathwayId = _pathway.Id,
            CreatedAtUtc = Now,
        });
        notification.Id.Should().NotBeEmpty();
    }

    [TestCase(MonitoringEvent.PathwayCreated, NotificationKind.MonitoredPathwayCreated, "Pathway created", "test.staff created a High priority Test template pathway.")]
    [TestCase(MonitoringEvent.StageFinished, NotificationKind.MonitoredStageFinished, "Stage finished", "test.staff finished a stage on a High priority Test template pathway.")]
    [TestCase(MonitoringEvent.PathwayCompleted, NotificationKind.MonitoredPathwayCompleted, "Pathway discharged", "test.staff discharged a High priority Test template pathway.")]
    [TestCase(MonitoringEvent.PathwayBlocked, NotificationKind.MonitoredPathwayBlocked, "Pathway on hold", "test.staff put a High priority Test template pathway on hold.")]
    [TestCase(MonitoringEvent.PathwayCancelled, NotificationKind.MonitoredPathwayCancelled, "Pathway cancelled", "test.staff cancelled a High priority Test template pathway.")]
    [TestCase(MonitoringEvent.PathwayAssigned, NotificationKind.MonitoredPathwayAssigned, "Staff assigned", "test.staff assigned themselves to a High priority Test template pathway.")]
    [TestCase(MonitoringEvent.StageUnlocked, NotificationKind.MonitoredStageUnlocked, "Stage unlocked", "test.staff unlocked a stage on a High priority Test template pathway.")]
    [TestCase(MonitoringEvent.DueDateChanged, NotificationKind.MonitoredDueDateChanged, "Target date changed", "test.staff changed the target date of a High priority Test template pathway.")]
    [TestCase(MonitoringEvent.StageFinishedWithoutVisit, NotificationKind.MonitoredStageFinishedWithoutVisit, "Stage finished without visit", "test.staff finished a stage on a High priority Test template pathway without its required visit.")]
    public void Create_EachEvent_HasItsKindAFixedTitleAndAPlainMessage(MonitoringEvent monitoringEvent, NotificationKind kind, string title, string message)
    {
        var notification = MonitoringNotifications.Create(OwnerId, ByStaff(monitoringEvent), _pathway, Now);

        notification.Kind.Should().Be(kind);
        notification.Title.Should().Be(title);
        notification.Message.Should().Be(message);
    }

    [Test]
    public void Create_EveryEvent_NeverCarriesThePathwaysTitleOrThePatient()
    {
        foreach (var monitoringEvent in Enum.GetValues<MonitoringEvent>())
        {
            var notification = MonitoringNotifications.Create(OwnerId, ByStaff(monitoringEvent), _pathway, Now);

            foreach (var text in new[] { notification.Title, notification.Message })
            {
                text.Should().NotContain(SecretTitle);
                text.Should().NotContain(_pathway.Patient.FirstName).And.NotContain(_pathway.Patient.LastName);
            }
        }
    }

    [Test]
    public void Create_SomeoneAssignedAnother_NamesBoth()
    {
        var activity = new MonitoredActivity(MonitoringEvent.PathwayAssigned, Admin.Username, Staff.UserId, Staff.Username);

        MonitoringNotifications.Create(OwnerId, activity, _pathway, Now).Message
            .Should().Be($"{Admin.Username} assigned {Staff.Username} to a High priority {TemplateName} pathway.");
    }

    [Test]
    public void Create_AssignedAccountSinceDeleted_SaysSo()
    {
        var activity = new MonitoredActivity(MonitoringEvent.PathwayAssigned, Admin.Username, Staff.UserId, string.Empty);

        MonitoringNotifications.Create(OwnerId, activity, _pathway, Now).Message.Should().StartWith($"{Admin.Username} assigned a deleted user to");
    }

    [Test]
    public void Create_DueDateMoved_GivesBothDates()
    {
        var activity = ByStaff(MonitoringEvent.DueDateChanged) with { DueDateFrom = new DateOnly(2026, 6, 22), DueDateTo = new DateOnly(2026, 7, 1) };

        MonitoringNotifications.Create(OwnerId, activity, _pathway, Now).Message
            .Should().Be($"{Staff.Username} moved the target date of a High priority {TemplateName} pathway from 22 Jun 2026 to 1 Jul 2026.");
    }

    [Test]
    public void Create_UrgentPathwayWithNoTemplate_ReadsWell()
    {
        var pathway = CreatePathway(priority: Priority.Urgent);
        pathway.TemplateName = string.Empty;

        MonitoringNotifications.Create(OwnerId, ByStaff(MonitoringEvent.PathwayCreated), pathway, Now).Message
            .Should().Be($"{Staff.Username} created an Urgent priority pathway.");
    }

    [Test]
    public void Create_VeryLongNames_KeepsTheMessageWithinItsColumn()
    {
        var activity = new MonitoredActivity(MonitoringEvent.PathwayAssigned, new string('a', 300), Staff.UserId, new string('b', 300));

        var message = MonitoringNotifications.Create(OwnerId, activity, _pathway, Now).Message;

        message.Should().HaveLength(MonitoringNotifications.MaxMessageLength).And.EndWith("…");
    }

    [Test]
    public void KindAndTitle_UnknownEvent_Throw()
    {
        var unknown = (MonitoringEvent)99;

        FluentActions.Invoking(() => MonitoringNotifications.KindOf(unknown)).Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => MonitoringNotifications.TitleOf(unknown)).Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => MonitoringNotifications.MessageOf(ByStaff(unknown), _pathway)).Should().Throw<ArgumentOutOfRangeException>();
    }
}
