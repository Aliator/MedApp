using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Monitoring.Commands.SetMonitoringRuleEnabled;
using MedApp.Domain.Monitoring;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.MonitoringTestConstants;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;

namespace MedApp.UnitTests.Application.Monitoring.Commands.SetMonitoringRuleEnabled;

[TestFixture]
public sealed class SetMonitoringRuleEnabledHandlerTests : MonitoringHandlerTestBase
{
    private SetMonitoringRuleEnabledHandler Handler => Fixture.Create<SetMonitoringRuleEnabledHandler>();

    [Test]
    public async Task Handle_RuleNamingDeletedThings_SwitchesItOffWithoutCheckingThem()
    {
        var rule = CreateRule(MonitoringEvent.StageFinished, subjectUserId: Guid.NewGuid(), templateId: Guid.NewGuid(),
            stageDefinitionId: Guid.NewGuid(), id: RuleId);
        Rules.Setup(r => r.GetAsync(RuleId, OwnerId, It.IsAny<CancellationToken>())).ReturnsAsync(rule);
        using var cts = new CancellationTokenSource();

        var response = await Handler.Handle(new SetMonitoringRuleEnabledCommand(RuleId, OwnerId, false), cts.Token);

        rule.IsEnabled.Should().BeFalse();
        rule.UpdatedAtUtc.Should().Be(Now);
        Rules.Verify(r => r.UpdateAsync(rule, cts.Token), Times.Once);
        response.Should().BeEquivalentTo(new { Id = RuleId, IsEnabled = false, SubjectUsername = (string?)null });
        Stages.Verify(s => s.GetStageDefinitionByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_AlreadyThatWay_SavesNothing()
    {
        Rules.Setup(r => r.GetAsync(RuleId, OwnerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateRule(MonitoringEvent.ProcedureCreated, id: RuleId));

        var response = await Handler.Handle(new SetMonitoringRuleEnabledCommand(RuleId, OwnerId, true), CancellationToken.None);

        response.IsEnabled.Should().BeTrue();
        Rules.Verify(r => r.UpdateAsync(It.IsAny<MonitoringRule>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_SomeoneElsesOrMissingRule_ThrowsNotFound()
    {
        var act = () => Handler.Handle(new SetMonitoringRuleEnabledCommand(RuleId, OtherOwnerId, false), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
