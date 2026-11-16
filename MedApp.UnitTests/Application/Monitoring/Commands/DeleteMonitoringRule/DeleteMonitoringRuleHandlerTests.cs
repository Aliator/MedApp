using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Monitoring.Commands.DeleteMonitoringRule;
using MedApp.Domain.Monitoring;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.MonitoringTestConstants;

namespace MedApp.UnitTests.Application.Monitoring.Commands.DeleteMonitoringRule;

[TestFixture]
public sealed class DeleteMonitoringRuleHandlerTests : MonitoringHandlerTestBase
{
    private DeleteMonitoringRuleHandler Handler => Fixture.Create<DeleteMonitoringRuleHandler>();

    [Test]
    public async Task Handle_TheirRule_DeletesIt()
    {
        var rule = CreateRule(MonitoringEvent.ProcedureBlocked, id: RuleId);
        Rules.Setup(r => r.GetAsync(RuleId, OwnerId, It.IsAny<CancellationToken>())).ReturnsAsync(rule);
        using var cts = new CancellationTokenSource();

        await Handler.Handle(new DeleteMonitoringRuleCommand(RuleId, OwnerId), cts.Token);

        Rules.Verify(r => r.DeleteAsync(rule, cts.Token), Times.Once);
    }

    [Test]
    public async Task Handle_SomeoneElsesOrMissingRule_ThrowsNotFound()
    {
        var act = () => Handler.Handle(new DeleteMonitoringRuleCommand(RuleId, OtherOwnerId), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        Rules.Verify(r => r.DeleteAsync(It.IsAny<MonitoringRule>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
