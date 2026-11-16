using FluentAssertions;
using MedApp.Application.Monitoring.Common;
using MedApp.Domain.Monitoring;
using MedApp.Domain.Procedures.PatientProcedures;
using static MedApp.UnitTests.Common.Constants.MonitoringTestConstants;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;
using Priority = MedApp.Domain.Procedures.PatientProcedures.PatientProcedurePriority;

namespace MedApp.UnitTests.Application.Monitoring.Common;

[TestFixture]
public sealed class MonitoringRuleMatcherTests
{
    private static readonly MonitoredActivity StaffFinishedConsultation = new(
        MonitoringEvent.StageFinished, Staff.Username, Staff.UserId, Staff.Username, ConsultationDefinitionId, ConsultationStage);

    private static readonly MonitoredActivity StaffCreated = new(
        MonitoringEvent.ProcedureCreated, Staff.Username, Staff.UserId, Staff.Username);

    private PatientProcedure _procedure = null!;

    [SetUp]
    public void SetUp()
    {
        _procedure = CreateProcedure(priority: Priority.High);
        _procedure.TemplateId = TemplateId;
    }

    [Test]
    public void Matches_RuleWithNoConditions_MatchesItsEventByAnyone()
    {
        MonitoringRuleMatcher.Matches(CreateRule(MonitoringEvent.StageFinished), StaffFinishedConsultation, _procedure).Should().BeTrue();
    }

    [Test]
    public void Matches_AnotherEvent_DoesNotMatch()
    {
        MonitoringRuleMatcher.Matches(CreateRule(MonitoringEvent.ProcedureCompleted), StaffFinishedConsultation, _procedure).Should().BeFalse();
    }

    [Test]
    public void Matches_SwitchedOff_DoesNotMatch()
    {
        MonitoringRuleMatcher.Matches(CreateRule(MonitoringEvent.StageFinished, isEnabled: false), StaffFinishedConsultation, _procedure)
            .Should().BeFalse();
    }

    [Test]
    public void Matches_ThatPerson_Matches_SomeoneElse_DoesNot()
    {
        MonitoringRuleMatcher.Matches(CreateRule(MonitoringEvent.StageFinished, subjectUserId: Staff.UserId), StaffFinishedConsultation, _procedure)
            .Should().BeTrue();
        MonitoringRuleMatcher.Matches(CreateRule(MonitoringEvent.StageFinished, subjectUserId: OtherUserId), StaffFinishedConsultation, _procedure)
            .Should().BeFalse();
    }

    [TestCase(Priority.Low, true)]
    [TestCase(Priority.Medium, true)]
    [TestCase(Priority.High, true)]
    [TestCase(Priority.Urgent, false)]
    public void Matches_MinimumPriority_MatchesThatPriorityOrHigher(Priority minimum, bool expected)
    {
        MonitoringRuleMatcher.Matches(CreateRule(MonitoringEvent.StageFinished, minimumPriority: minimum), StaffFinishedConsultation, _procedure)
            .Should().Be(expected);
    }

    [Test]
    public void Matches_PriorityLoweredInTheSameEdit_MatchesOnThePriorityBefore()
    {
        _procedure.Priority = Priority.Low;
        var moved = new MonitoredActivity(MonitoringEvent.DueDateChanged, Staff.Username, Staff.UserId, Staff.Username,
            PriorityBefore: Priority.Urgent);

        MonitoringRuleMatcher.Matches(CreateRule(MonitoringEvent.DueDateChanged, minimumPriority: Priority.Urgent), moved, _procedure)
            .Should().BeTrue();
        MonitoringRuleMatcher.PriorityOf(moved, _procedure).Should().Be(Priority.Urgent);
    }

    [Test]
    public void PriorityOf_PriorityRaised_IsTheOneAfter()
    {
        var moved = new MonitoredActivity(MonitoringEvent.DueDateChanged, Staff.Username, Staff.UserId, Staff.Username,
            PriorityBefore: Priority.Low);

        MonitoringRuleMatcher.PriorityOf(moved, _procedure).Should().Be(Priority.High);
    }

    [Test]
    public void Match_AdminWatchingAssignmentsIsAssigned_IsToldOnceByTheAssignmentNotification()
    {
        var assigned = new MonitoredActivity(MonitoringEvent.ProcedureAssigned, Admin.Username, OwnerId, "test.owner");
        var rules = new[] { CreateRule(MonitoringEvent.ProcedureAssigned), CreateRule(MonitoringEvent.ProcedureAssigned, ownerId: OtherOwnerId) };

        var matches = MonitoringRuleMatcher.Match(rules, [assigned], _procedure, Admin.UserId);

        matches.Should().ContainSingle().Which.OwnerUserId.Should().Be(OtherOwnerId);
    }

    [Test]
    public void Matches_Template_MatchesOnlyProceduresFromIt()
    {
        MonitoringRuleMatcher.Matches(CreateRule(MonitoringEvent.StageFinished, templateId: TemplateId), StaffFinishedConsultation, _procedure)
            .Should().BeTrue();
        MonitoringRuleMatcher.Matches(CreateRule(MonitoringEvent.StageFinished, templateId: Guid.NewGuid()), StaffFinishedConsultation, _procedure)
            .Should().BeFalse();
    }

    [Test]
    public void Matches_Stage_MatchesOnlyStagesMadeFromThatDefinition()
    {
        MonitoringRuleMatcher.Matches(
                CreateRule(MonitoringEvent.StageFinished, stageDefinitionId: ConsultationDefinitionId), StaffFinishedConsultation, _procedure)
            .Should().BeTrue();
        MonitoringRuleMatcher.Matches(
                CreateRule(MonitoringEvent.StageFinished, stageDefinitionId: ProcedureDefinitionId), StaffFinishedConsultation, _procedure)
            .Should().BeFalse();
    }

    [Test]
    public void Matches_EveryConditionHolds_Matches()
    {
        var rule = CreateRule(MonitoringEvent.StageFinished, subjectUserId: Staff.UserId, minimumPriority: Priority.High,
            templateId: TemplateId, stageDefinitionId: ConsultationDefinitionId);

        MonitoringRuleMatcher.Matches(rule, StaffFinishedConsultation, _procedure).Should().BeTrue();
    }

    [Test]
    public void Match_SeveralOfOneAdminsRulesMatch_NotifiesThemOnce()
    {
        var rules = new[]
        {
            CreateRule(MonitoringEvent.StageFinished),
            CreateRule(MonitoringEvent.StageFinished, subjectUserId: Staff.UserId),
            CreateRule(MonitoringEvent.StageFinished, stageDefinitionId: ConsultationDefinitionId),
        };

        var matches = MonitoringRuleMatcher.Match(rules, [StaffFinishedConsultation], _procedure, Staff.UserId);

        matches.Should().Equal(new MonitoringMatch(OwnerId, StaffFinishedConsultation));
    }

    [Test]
    public void Match_TwoAdminsRulesMatch_NotifiesEach()
    {
        var rules = new[] { CreateRule(MonitoringEvent.StageFinished), CreateRule(MonitoringEvent.StageFinished, ownerId: OtherOwnerId) };

        var matches = MonitoringRuleMatcher.Match(rules, [StaffFinishedConsultation], _procedure, Staff.UserId);

        matches.Select(m => m.OwnerUserId).Should().Equal(OwnerId, OtherOwnerId);
    }

    [Test]
    public void Match_AdminMadeTheChangeThemselves_IsNotToldAboutIt()
    {
        var rules = new[] { CreateRule(MonitoringEvent.StageFinished, ownerId: Staff.UserId), CreateRule(MonitoringEvent.StageFinished) };

        var matches = MonitoringRuleMatcher.Match(rules, [StaffFinishedConsultation], _procedure, Staff.UserId);

        matches.Should().ContainSingle().Which.OwnerUserId.Should().Be(OwnerId);
    }

    [Test]
    public void Match_ChangeDidTwoWatchedThings_TellsEachAdminAboutTheFirstTheirRulesMatch()
    {
        var rules = new[]
        {
            CreateRule(MonitoringEvent.StageFinished),
            CreateRule(MonitoringEvent.ProcedureCreated),
            CreateRule(MonitoringEvent.StageFinished, ownerId: OtherOwnerId),
        };

        var matches = MonitoringRuleMatcher.Match(rules, [StaffCreated, StaffFinishedConsultation], _procedure, Staff.UserId);

        matches.Should().Equal(new MonitoringMatch(OwnerId, StaffCreated), new MonitoringMatch(OtherOwnerId, StaffFinishedConsultation));
    }

    [Test]
    public void Match_NoRuleMatches_IsEmpty()
    {
        MonitoringRuleMatcher.Match([CreateRule(MonitoringEvent.ProcedureBlocked)], [StaffCreated], _procedure, Staff.UserId)
            .Should().BeEmpty();
    }
}
