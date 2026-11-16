using AutoFixture;
using FluentAssertions;
using FluentValidation;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Monitoring.Common;
using MedApp.Application.Monitoring.Commands.UpdateMonitoringRule;
using MedApp.Domain.Monitoring;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.MonitoringTestConstants;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;
using Priority = MedApp.Domain.Procedures.PatientProcedures.PatientProcedurePriority;

namespace MedApp.UnitTests.Application.Monitoring.Commands.UpdateMonitoringRule;

[TestFixture]
public sealed class UpdateMonitoringRuleHandlerTests : MonitoringHandlerTestBase
{
    private UpdateMonitoringRuleHandler Handler => Fixture.Create<UpdateMonitoringRuleHandler>();

    [Test]
    public async Task Handle_TheirRule_ReplacesItsFieldsAndKeepsWhenItWasMade()
    {
        var rule = CreateRule(MonitoringEvent.ProcedureCreated, id: RuleId);
        Rules.Setup(r => r.GetAsync(RuleId, OwnerId, It.IsAny<CancellationToken>())).ReturnsAsync(rule);
        using var cts = new CancellationTokenSource();

        var response = await Handler.Handle(
            new UpdateMonitoringRuleCommand(RuleId, OwnerId, Admin.UserId, "StageUnlocked", "Urgent", TemplateId, ConsultationDefinitionId, false),
            cts.Token);

        rule.Should().BeEquivalentTo(new
        {
            Id = RuleId,
            OwnerUserId = OwnerId,
            SubjectUserId = (Guid?)Admin.UserId,
            Event = MonitoringEvent.StageUnlocked,
            MinimumPriority = (Priority?)Priority.Urgent,
            TemplateId = (Guid?)TemplateId,
            StageDefinitionId = (Guid?)ConsultationDefinitionId,
            IsEnabled = false,
            CreatedAtUtc,
            UpdatedAtUtc = Now,
        });
        Rules.Verify(r => r.UpdateAsync(rule, cts.Token), Times.Once);
        response.Should().BeEquivalentTo(new { Id = RuleId, SubjectUsername = Admin.Username, Event = "StageUnlocked", IsEnabled = false });
    }

    [Test]
    public async Task Handle_SwitchedOffWithConditionsCleared_ClearsThem()
    {
        var rule = CreateRule(MonitoringEvent.StageFinished, subjectUserId: Staff.UserId, minimumPriority: Priority.High, id: RuleId);
        Rules.Setup(r => r.GetAsync(RuleId, OwnerId, It.IsAny<CancellationToken>())).ReturnsAsync(rule);

        await Handler.Handle(new UpdateMonitoringRuleCommand(RuleId, OwnerId, null, "StageFinished", null, null, null, true), CancellationToken.None);

        rule.SubjectUserId.Should().BeNull();
        rule.MinimumPriority.Should().BeNull();
    }

    [Test]
    public async Task Handle_SomeoneElsesOrMissingRule_ThrowsNotFound()
    {
        var act = () => Handler.Handle(
            new UpdateMonitoringRuleCommand(RuleId, OtherOwnerId, null, "StageFinished", null, null, null, true), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        Rules.Verify(r => r.UpdateAsync(It.IsAny<MonitoringRule>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_RuleKeepsNamingADeletedAccount_SavesTheOtherChanges()
    {
        var deleted = Guid.NewGuid();
        var rule = CreateRule(MonitoringEvent.StageFinished, subjectUserId: deleted, templateId: Guid.NewGuid(),
            stageDefinitionId: Guid.NewGuid(), id: RuleId);
        Rules.Setup(r => r.GetAsync(RuleId, OwnerId, It.IsAny<CancellationToken>())).ReturnsAsync(rule);

        var response = await Handler.Handle(new UpdateMonitoringRuleCommand(RuleId, OwnerId, deleted, "StageFinished", "Urgent",
            rule.TemplateId, rule.StageDefinitionId, false), CancellationToken.None);

        Rules.Verify(r => r.UpdateAsync(rule, It.IsAny<CancellationToken>()), Times.Once);
        rule.MinimumPriority.Should().Be(Priority.Urgent);
        response.Should().BeEquivalentTo(new { SubjectUserId = (Guid?)deleted, SubjectUsername = (string?)null, TemplateName = (string?)null, StageName = (string?)null });
    }

    [Test]
    public async Task Handle_ChangedToADeletedAccount_ThrowsNotFound()
    {
        Rules.Setup(r => r.GetAsync(RuleId, OwnerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateRule(MonitoringEvent.StageFinished, subjectUserId: Staff.UserId, id: RuleId));

        var act = () => Handler.Handle(
            new UpdateMonitoringRuleCommand(RuleId, OwnerId, Guid.NewGuid(), "StageFinished", null, null, null, true), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage("User*");
    }

    [Test]
    public async Task Handle_ChangedToStageUnlockedForSomeoneNotAnAdmin_ThrowsValidation()
    {
        Rules.Setup(r => r.GetAsync(RuleId, OwnerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateRule(MonitoringEvent.StageFinished, subjectUserId: Staff.UserId, id: RuleId));

        var act = () => Handler.Handle(
            new UpdateMonitoringRuleCommand(RuleId, OwnerId, Staff.UserId, "StageUnlocked", null, null, null, true), CancellationToken.None);

        (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be(MonitoringRuleInput.UnlockNeedsAdmin);
        Rules.Verify(r => r.UpdateAsync(It.IsAny<MonitoringRule>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_StageUnlockedForAnAdmin_Saves()
    {
        var rule = CreateRule(MonitoringEvent.StageFinished, subjectUserId: Admin.UserId, id: RuleId);
        Rules.Setup(r => r.GetAsync(RuleId, OwnerId, It.IsAny<CancellationToken>())).ReturnsAsync(rule);

        await Handler.Handle(new UpdateMonitoringRuleCommand(RuleId, OwnerId, Admin.UserId, "StageUnlocked", null, null, null, true), CancellationToken.None);

        rule.Event.Should().Be(MonitoringEvent.StageUnlocked);
    }

    [Test]
    public async Task Handle_UnknownTemplate_ThrowsNotFoundAndChangesNothing()
    {
        Rules.Setup(r => r.GetAsync(RuleId, OwnerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateRule(MonitoringEvent.StageFinished, id: RuleId));

        var act = () => Handler.Handle(
            new UpdateMonitoringRuleCommand(RuleId, OwnerId, null, "StageFinished", null, Guid.NewGuid(), null, true), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        Rules.Verify(r => r.UpdateAsync(It.IsAny<MonitoringRule>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
