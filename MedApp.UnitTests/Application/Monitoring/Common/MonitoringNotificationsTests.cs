using FluentAssertions;
using MedApp.Application.Monitoring.Common;
using MedApp.Domain.Monitoring;
using MedApp.Domain.Notifications;
using MedApp.Domain.Procedures.PatientProcedures;
using static MedApp.UnitTests.Common.Constants.MonitoringTestConstants;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;
using Priority = MedApp.Domain.Procedures.PatientProcedures.PatientProcedurePriority;

namespace MedApp.UnitTests.Application.Monitoring.Common;

[TestFixture]
public sealed class MonitoringNotificationsTests
{
    private const string SecretTitle = "Synthetic free text title";

    private PatientProcedure _procedure = null!;

    [SetUp]
    public void SetUp() => _procedure = CreateProcedure(priority: Priority.High, title: SecretTitle);

    private static MonitoredActivity ByStaff(MonitoringEvent monitoringEvent) =>
        new(monitoringEvent, Staff.Username, Staff.UserId, Staff.Username);

    [Test]
    public void Create_StageFinished_SaysWhoFinishedWhichStageOnWhatKindOfProcedure()
    {
        var activity = ByStaff(MonitoringEvent.StageFinished) with { StageDefinitionId = ConsultationDefinitionId, StageName = BloodsStage };

        var notification = MonitoringNotifications.Create(OwnerId, activity, _procedure, Now);

        notification.Should().BeEquivalentTo(new Notification
        {
            Id = notification.Id,
            UserId = OwnerId,
            Kind = NotificationKind.MonitoredStageFinished,
            Title = "Stage finished",
            Message = $"{Staff.Username} finished the {BloodsStage} stage on a High priority {TemplateName} procedure.",
            ProcedureId = _procedure.Id,
            CreatedAtUtc = Now,
        });
        notification.Id.Should().NotBeEmpty();
    }

    [TestCase(MonitoringEvent.ProcedureCreated, NotificationKind.MonitoredProcedureCreated, "Procedure created", "test.staff created a High priority Test template procedure.")]
    [TestCase(MonitoringEvent.StageFinished, NotificationKind.MonitoredStageFinished, "Stage finished", "test.staff finished a stage on a High priority Test template procedure.")]
    [TestCase(MonitoringEvent.ProcedureCompleted, NotificationKind.MonitoredProcedureCompleted, "Procedure completed", "test.staff completed a High priority Test template procedure.")]
    [TestCase(MonitoringEvent.ProcedureBlocked, NotificationKind.MonitoredProcedureBlocked, "Procedure blocked", "test.staff blocked a High priority Test template procedure.")]
    [TestCase(MonitoringEvent.ProcedureCancelled, NotificationKind.MonitoredProcedureCancelled, "Procedure cancelled", "test.staff cancelled a High priority Test template procedure.")]
    [TestCase(MonitoringEvent.ProcedureAssigned, NotificationKind.MonitoredProcedureAssigned, "Staff assigned", "test.staff assigned themselves to a High priority Test template procedure.")]
    [TestCase(MonitoringEvent.StageUnlocked, NotificationKind.MonitoredStageUnlocked, "Stage unlocked", "test.staff unlocked a stage on a High priority Test template procedure.")]
    [TestCase(MonitoringEvent.DueDateChanged, NotificationKind.MonitoredDueDateChanged, "Due date changed", "test.staff changed the due date of a High priority Test template procedure.")]
    public void Create_EachEvent_HasItsKindAFixedTitleAndAPlainMessage(MonitoringEvent monitoringEvent, NotificationKind kind, string title, string message)
    {
        var notification = MonitoringNotifications.Create(OwnerId, ByStaff(monitoringEvent), _procedure, Now);

        notification.Kind.Should().Be(kind);
        notification.Title.Should().Be(title);
        notification.Message.Should().Be(message);
    }

    [Test]
    public void Create_EveryEvent_NeverCarriesTheProceduresTitleOrThePatient()
    {
        foreach (var monitoringEvent in Enum.GetValues<MonitoringEvent>())
        {
            var notification = MonitoringNotifications.Create(OwnerId, ByStaff(monitoringEvent), _procedure, Now);

            foreach (var text in new[] { notification.Title, notification.Message })
            {
                text.Should().NotContain(SecretTitle);
                text.Should().NotContain(_procedure.Patient.FirstName).And.NotContain(_procedure.Patient.LastName);
            }
        }
    }

    [Test]
    public void Create_SomeoneAssignedAnother_NamesBoth()
    {
        var activity = new MonitoredActivity(MonitoringEvent.ProcedureAssigned, Admin.Username, Staff.UserId, Staff.Username);

        MonitoringNotifications.Create(OwnerId, activity, _procedure, Now).Message
            .Should().Be($"{Admin.Username} assigned {Staff.Username} to a High priority {TemplateName} procedure.");
    }

    [Test]
    public void Create_AssignedAccountSinceDeleted_SaysSo()
    {
        var activity = new MonitoredActivity(MonitoringEvent.ProcedureAssigned, Admin.Username, Staff.UserId, string.Empty);

        MonitoringNotifications.Create(OwnerId, activity, _procedure, Now).Message.Should().StartWith($"{Admin.Username} assigned a deleted user to");
    }

    [Test]
    public void Create_DueDateMoved_GivesBothDates()
    {
        var activity = ByStaff(MonitoringEvent.DueDateChanged) with { DueDateFrom = new DateOnly(2026, 6, 22), DueDateTo = new DateOnly(2026, 7, 1) };

        MonitoringNotifications.Create(OwnerId, activity, _procedure, Now).Message
            .Should().Be($"{Staff.Username} moved the due date of a High priority {TemplateName} procedure from 22 Jun 2026 to 1 Jul 2026.");
    }

    [Test]
    public void Create_UrgentProcedureWithNoTemplate_ReadsWell()
    {
        var procedure = CreateProcedure(priority: Priority.Urgent);
        procedure.TemplateName = string.Empty;

        MonitoringNotifications.Create(OwnerId, ByStaff(MonitoringEvent.ProcedureCreated), procedure, Now).Message
            .Should().Be($"{Staff.Username} created an Urgent priority procedure.");
    }

    [Test]
    public void Create_VeryLongNames_KeepsTheMessageWithinItsColumn()
    {
        var activity = new MonitoredActivity(MonitoringEvent.ProcedureAssigned, new string('a', 300), Staff.UserId, new string('b', 300));

        var message = MonitoringNotifications.Create(OwnerId, activity, _procedure, Now).Message;

        message.Should().HaveLength(MonitoringNotifications.MaxMessageLength).And.EndWith("…");
    }

    [Test]
    public void KindAndTitle_UnknownEvent_Throw()
    {
        var unknown = (MonitoringEvent)99;

        FluentActions.Invoking(() => MonitoringNotifications.KindOf(unknown)).Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => MonitoringNotifications.TitleOf(unknown)).Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => MonitoringNotifications.MessageOf(ByStaff(unknown), _procedure)).Should().Throw<ArgumentOutOfRangeException>();
    }
}
