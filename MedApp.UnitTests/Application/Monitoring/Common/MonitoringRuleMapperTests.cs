using FluentAssertions;
using MedApp.Application.Monitoring.Common;
using MedApp.Contracts.Monitoring.Responses;
using MedApp.Domain.Monitoring;
using static MedApp.UnitTests.Common.Constants.MonitoringTestConstants;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using Priority = MedApp.Domain.Pathways.PatientPathways.PatientPathwayPriority;

namespace MedApp.UnitTests.Application.Monitoring.Common;

[TestFixture]
public sealed class MonitoringRuleMapperTests
{
    private static readonly MonitoringRuleNames Names = new(
        new Dictionary<Guid, string> { [Staff.UserId] = Staff.Username },
        new Dictionary<Guid, string> { [TemplateId] = TemplateName },
        new Dictionary<Guid, string> { [ConsultationDefinitionId] = ConsultationStage });

    [Test]
    public void ToResponse_EveryCondition_GivesEventsAndPrioritiesByNameAndWhatItNames()
    {
        var rule = CreateRule(MonitoringEvent.StageFinished, subjectUserId: Staff.UserId, minimumPriority: Priority.Urgent,
            templateId: TemplateId, stageDefinitionId: ConsultationDefinitionId, isEnabled: false, id: RuleId);

        MonitoringRuleMapper.ToResponse(rule, Names).Should().Be(new MonitoringRuleResponse(
            RuleId, Staff.UserId, Staff.Username, "StageFinished", "Urgent", TemplateId, TemplateName,
            ConsultationDefinitionId, ConsultationStage, false, CreatedAtUtc, CreatedAtUtc));
    }

    [Test]
    public void ToResponse_NoConditions_NamesNothing()
    {
        var response = MonitoringRuleMapper.ToResponse(CreateRule(MonitoringEvent.DueDateChanged, id: RuleId), Names);

        response.Should().Be(new MonitoringRuleResponse(
            RuleId, null, null, "DueDateChanged", null, null, null, null, null, true, CreatedAtUtc, CreatedAtUtc));
    }

    [Test]
    public void ToResponse_NamesSomethingSinceDeleted_KeepsItsIdWithNoName()
    {
        var gone = Guid.NewGuid();

        var response = MonitoringRuleMapper.ToResponse(
            CreateRule(MonitoringEvent.StageUnlocked, subjectUserId: gone, templateId: gone, stageDefinitionId: gone), Names);

        response.Should().BeEquivalentTo(new
        {
            SubjectUserId = (Guid?)gone, SubjectUsername = (string?)null,
            TemplateId = (Guid?)gone, TemplateName = (string?)null,
            StageDefinitionId = (Guid?)gone, StageName = (string?)null,
        });
    }

    [Test]
    public void ToResponse_SeveralRules_KeepsTheirOrder()
    {
        var rules = new[] { CreateRule(MonitoringEvent.PathwayBlocked), CreateRule(MonitoringEvent.PathwayCancelled) };

        MonitoringRuleMapper.ToResponse(rules, Names).Select(r => r.Event).Should().Equal("PathwayBlocked", "PathwayCancelled");
    }
}
