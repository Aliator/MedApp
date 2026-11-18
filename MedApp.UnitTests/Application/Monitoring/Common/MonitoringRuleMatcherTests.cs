using FluentAssertions;
using MedApp.Application.Monitoring.Common;
using MedApp.Domain.Monitoring;
using MedApp.Domain.Pathways.PatientPathways;
using static MedApp.UnitTests.Common.Constants.MonitoringTestConstants;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using Priority = MedApp.Domain.Pathways.PatientPathways.PatientPathwayPriority;

namespace MedApp.UnitTests.Application.Monitoring.Common;

[TestFixture]
public sealed class MonitoringRuleMatcherTests
{
    private static readonly MonitoredActivity StaffFinishedConsultation = new(
        MonitoringEvent.StageFinished, Staff.Username, Staff.UserId, Staff.Username, ConsultationDefinitionId, ConsultationStage);

    private static readonly MonitoredActivity StaffCreated = new(
        MonitoringEvent.PathwayCreated, Staff.Username, Staff.UserId, Staff.Username);

    private PatientPathway _pathway = null!;

    [SetUp]
    public void SetUp()
    {
        _pathway = CreatePathway(priority: Priority.High);
        _pathway.TemplateId = TemplateId;
    }

    [Test]
    public void Matches_RuleWithNoConditions_MatchesItsEventByAnyone()
    {
        MonitoringRuleMatcher.Matches(CreateRule(MonitoringEvent.StageFinished), StaffFinishedConsultation, _pathway).Should().BeTrue();
    }

    [Test]
    public void Matches_AnotherEvent_DoesNotMatch()
    {
        MonitoringRuleMatcher.Matches(CreateRule(MonitoringEvent.PathwayCompleted), StaffFinishedConsultation, _pathway).Should().BeFalse();
    }

    [Test]
    public void Matches_SwitchedOff_DoesNotMatch()
    {
        MonitoringRuleMatcher.Matches(CreateRule(MonitoringEvent.StageFinished, isEnabled: false), StaffFinishedConsultation, _pathway)
            .Should().BeFalse();
    }

    [Test]
    public void Matches_ThatPerson_Matches_SomeoneElse_DoesNot()
    {
        MonitoringRuleMatcher.Matches(CreateRule(MonitoringEvent.StageFinished, subjectUserId: Staff.UserId), StaffFinishedConsultation, _pathway)
            .Should().BeTrue();
        MonitoringRuleMatcher.Matches(CreateRule(MonitoringEvent.StageFinished, subjectUserId: OtherUserId), StaffFinishedConsultation, _pathway)
            .Should().BeFalse();
    }

    [TestCase(Priority.Low, true)]
    [TestCase(Priority.Medium, true)]
    [TestCase(Priority.High, true)]
    [TestCase(Priority.Urgent, false)]
    public void Matches_MinimumPriority_MatchesThatPriorityOrHigher(Priority minimum, bool expected)
    {
        MonitoringRuleMatcher.Matches(CreateRule(MonitoringEvent.StageFinished, minimumPriority: minimum), StaffFinishedConsultation, _pathway)
            .Should().Be(expected);
    }

    [Test]
    public void Matches_PriorityLoweredInTheSameEdit_MatchesOnThePriorityBefore()
    {
        _pathway.Priority = Priority.Low;
        var moved = new MonitoredActivity(MonitoringEvent.DueDateChanged, Staff.Username, Staff.UserId, Staff.Username,
            PriorityBefore: Priority.Urgent);

        MonitoringRuleMatcher.Matches(CreateRule(MonitoringEvent.DueDateChanged, minimumPriority: Priority.Urgent), moved, _pathway)
            .Should().BeTrue();
        MonitoringRuleMatcher.PriorityOf(moved, _pathway).Should().Be(Priority.Urgent);
    }

    [Test]
    public void PriorityOf_PriorityRaised_IsTheOneAfter()
    {
        var moved = new MonitoredActivity(MonitoringEvent.DueDateChanged, Staff.Username, Staff.UserId, Staff.Username,
            PriorityBefore: Priority.Low);

        MonitoringRuleMatcher.PriorityOf(moved, _pathway).Should().Be(Priority.High);
    }

    [Test]
    public void Match_AdminWatchingAssignmentsIsAssigned_IsToldOnceByTheAssignmentNotification()
    {
        var assigned = new MonitoredActivity(MonitoringEvent.PathwayAssigned, Admin.Username, OwnerId, "test.owner");
        var rules = new[] { CreateRule(MonitoringEvent.PathwayAssigned), CreateRule(MonitoringEvent.PathwayAssigned, ownerId: OtherOwnerId) };

        var matches = MonitoringRuleMatcher.Match(rules, [assigned], _pathway, Admin.UserId);

        matches.Should().ContainSingle().Which.OwnerUserId.Should().Be(OtherOwnerId);
    }

    [Test]
    public void Matches_Template_MatchesOnlyPathwaysFromIt()
    {
        MonitoringRuleMatcher.Matches(CreateRule(MonitoringEvent.StageFinished, templateId: TemplateId), StaffFinishedConsultation, _pathway)
            .Should().BeTrue();
        MonitoringRuleMatcher.Matches(CreateRule(MonitoringEvent.StageFinished, templateId: Guid.NewGuid()), StaffFinishedConsultation, _pathway)
            .Should().BeFalse();
    }

    [Test]
    public void Matches_Stage_MatchesOnlyStagesMadeFromThatDefinition()
    {
        MonitoringRuleMatcher.Matches(
                CreateRule(MonitoringEvent.StageFinished, stageDefinitionId: ConsultationDefinitionId), StaffFinishedConsultation, _pathway)
            .Should().BeTrue();
        MonitoringRuleMatcher.Matches(
                CreateRule(MonitoringEvent.StageFinished, stageDefinitionId: PathwayDefinitionId), StaffFinishedConsultation, _pathway)
            .Should().BeFalse();
    }

    [Test]
    public void Matches_EveryConditionHolds_Matches()
    {
        var rule = CreateRule(MonitoringEvent.StageFinished, subjectUserId: Staff.UserId, minimumPriority: Priority.High,
            templateId: TemplateId, stageDefinitionId: ConsultationDefinitionId);

        MonitoringRuleMatcher.Matches(rule, StaffFinishedConsultation, _pathway).Should().BeTrue();
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

        var matches = MonitoringRuleMatcher.Match(rules, [StaffFinishedConsultation], _pathway, Staff.UserId);

        matches.Should().Equal(new MonitoringMatch(OwnerId, StaffFinishedConsultation));
    }

    /// <summary>
    /// A Required stage finished without its visit is both events, the bypass first: an admin watching both is told
    /// once, of the bypass; one watching only stages finished is told of that.
    /// </summary>
    [Test]
    public void Match_FinishedWithoutItsVisit_WatchingBoth_IsToldOfTheBypass_WatchingFinishesIsToldOfTheFinish()
    {
        var bypass = StaffFinishedConsultation with { Event = MonitoringEvent.StageFinishedWithoutVisit };
        var rules = new[]
        {
            CreateRule(MonitoringEvent.StageFinished),
            CreateRule(MonitoringEvent.StageFinishedWithoutVisit, stageDefinitionId: ConsultationDefinitionId),
            CreateRule(MonitoringEvent.StageFinished, ownerId: OtherOwnerId),
        };

        var matches = MonitoringRuleMatcher.Match(rules, [bypass, StaffFinishedConsultation], _pathway, Admin.UserId);

        matches.Should().Equal(new MonitoringMatch(OwnerId, bypass), new MonitoringMatch(OtherOwnerId, StaffFinishedConsultation));
    }

    [Test]
    public void Match_TwoAdminsRulesMatch_NotifiesEach()
    {
        var rules = new[] { CreateRule(MonitoringEvent.StageFinished), CreateRule(MonitoringEvent.StageFinished, ownerId: OtherOwnerId) };

        var matches = MonitoringRuleMatcher.Match(rules, [StaffFinishedConsultation], _pathway, Staff.UserId);

        matches.Select(m => m.OwnerUserId).Should().Equal(OwnerId, OtherOwnerId);
    }

    [Test]
    public void Match_AdminMadeTheChangeThemselves_IsNotToldAboutIt()
    {
        var rules = new[] { CreateRule(MonitoringEvent.StageFinished, ownerId: Staff.UserId), CreateRule(MonitoringEvent.StageFinished) };

        var matches = MonitoringRuleMatcher.Match(rules, [StaffFinishedConsultation], _pathway, Staff.UserId);

        matches.Should().ContainSingle().Which.OwnerUserId.Should().Be(OwnerId);
    }

    [Test]
    public void Match_ChangeDidTwoWatchedThings_TellsEachAdminAboutTheFirstTheirRulesMatch()
    {
        var rules = new[]
        {
            CreateRule(MonitoringEvent.StageFinished),
            CreateRule(MonitoringEvent.PathwayCreated),
            CreateRule(MonitoringEvent.StageFinished, ownerId: OtherOwnerId),
        };

        var matches = MonitoringRuleMatcher.Match(rules, [StaffCreated, StaffFinishedConsultation], _pathway, Staff.UserId);

        matches.Should().Equal(new MonitoringMatch(OwnerId, StaffCreated), new MonitoringMatch(OtherOwnerId, StaffFinishedConsultation));
    }

    [Test]
    public void Match_NoRuleMatches_IsEmpty()
    {
        MonitoringRuleMatcher.Match([CreateRule(MonitoringEvent.PathwayBlocked)], [StaffCreated], _pathway, Staff.UserId)
            .Should().BeEmpty();
    }

    [Test]
    public void Matches_ThatSpecialty_Matches_AnotherDoesNot()
    {
        MonitoringRuleMatcher.Matches(CreateRule(MonitoringEvent.StageFinished, specialtyId: _pathway.SpecialtyId), StaffFinishedConsultation, _pathway)
            .Should().BeTrue();
        MonitoringRuleMatcher.Matches(CreateRule(MonitoringEvent.StageFinished, specialtyId: Guid.NewGuid()), StaffFinishedConsultation, _pathway)
            .Should().BeFalse();
    }
}
