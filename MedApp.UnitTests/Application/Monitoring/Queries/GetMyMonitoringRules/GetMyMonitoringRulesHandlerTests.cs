using AutoFixture;
using FluentAssertions;
using MedApp.Application.Monitoring.Queries.GetMyMonitoringRules;
using MedApp.Contracts.Monitoring.Responses;
using MedApp.Domain.Monitoring;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.MonitoringTestConstants;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;

namespace MedApp.UnitTests.Application.Monitoring.Queries.GetMyMonitoringRules;

[TestFixture]
public sealed class GetMyMonitoringRulesHandlerTests : MonitoringHandlerTestBase
{
    private GetMyMonitoringRulesHandler Handler => Fixture.Create<GetMyMonitoringRulesHandler>();

    [Test]
    public async Task Handle_TheirRules_ReturnsThemInOrderWithWhatTheyNameNow()
    {
        var deletedUserId = Guid.NewGuid();
        var deletedTemplateId = Guid.NewGuid();
        var deletedStageId = Guid.NewGuid();
        var first = CreateRule(MonitoringEvent.StageFinished, subjectUserId: Staff.UserId, templateId: TemplateId, stageDefinitionId: ConsultationDefinitionId);
        var second = CreateRule(MonitoringEvent.StageUnlocked, subjectUserId: deletedUserId, templateId: deletedTemplateId, stageDefinitionId: deletedStageId);
        var third = CreateRule(MonitoringEvent.ProcedureCreated);
        using var cts = new CancellationTokenSource();
        Rules.Setup(r => r.GetForOwnerAsync(OwnerId, cts.Token)).ReturnsAsync([first, second, third]);

        var response = await Handler.Handle(new GetMyMonitoringRulesQuery(OwnerId), cts.Token);

        response.Select(r => r.Id).Should().Equal(first.Id, second.Id, third.Id);
        response[0].Should().BeEquivalentTo(new { SubjectUsername = Staff.Username, TemplateName, StageName = ConsultationStage });
        response[1].Should().BeEquivalentTo(new
        {
            SubjectUserId = (Guid?)deletedUserId, SubjectUsername = (string?)null,
            TemplateId = (Guid?)deletedTemplateId, TemplateName = (string?)null,
            StageDefinitionId = (Guid?)deletedStageId, StageName = (string?)null,
        });
        response[2].Should().BeEquivalentTo(new { SubjectUserId = (Guid?)null, SubjectUsername = (string?)null, Event = "ProcedureCreated" });
        // Every name in one lookup each.
        Identity.Verify(i => i.GetUsernamesByIdAsync(It.IsAny<IEnumerable<Guid>>(), cts.Token), Times.Once);
        Stages.Verify(s => s.GetStageTemplateNamesAsync(It.IsAny<IEnumerable<Guid>>(), cts.Token), Times.Once);
        Stages.Verify(s => s.GetStageDefinitionsByIdAsync(It.IsAny<IEnumerable<Guid>>(), cts.Token), Times.Once);
    }

    [Test]
    public async Task Handle_NoRules_ReturnsNone()
    {
        Rules.Setup(r => r.GetForOwnerAsync(OwnerId, It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var response = await Handler.Handle(new GetMyMonitoringRulesQuery(OwnerId), CancellationToken.None);

        response.Should().BeEquivalentTo(Array.Empty<MonitoringRuleResponse>());
    }
}
