using AutoFixture;
using FluentAssertions;
using FluentValidation;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Monitoring.Commands.CreateMonitoringRule;
using MedApp.Application.Monitoring.Common;
using MedApp.Contracts.Monitoring.Responses;
using MedApp.Domain.Monitoring;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.MonitoringTestConstants;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using Priority = MedApp.Domain.Pathways.PatientPathways.PatientPathwayPriority;

namespace MedApp.UnitTests.Application.Monitoring.Commands.CreateMonitoringRule;

[TestFixture]
public sealed class CreateMonitoringRuleHandlerTests : MonitoringHandlerTestBase
{
    private CreateMonitoringRuleHandler Handler => Fixture.Create<CreateMonitoringRuleHandler>();

    private static CreateMonitoringRuleCommand Command(Guid? subject = null, Guid? template = null, Guid? stage = null) =>
        new(OwnerId, subject, "stageFinished", "high", template, stage, true);

    [Test]
    public async Task Handle_EveryCondition_SavesTheRuleForTheAdminAndReturnsItWithNames()
    {
        MonitoringRule? saved = null;
        Rules.Setup(r => r.AddAsync(It.IsAny<MonitoringRule>(), It.IsAny<CancellationToken>()))
            .Callback((MonitoringRule rule, CancellationToken _) => saved = rule)
            .Returns(Task.CompletedTask);
        using var cts = new CancellationTokenSource();

        var response = await Handler.Handle(Command(Staff.UserId, TemplateId, ConsultationDefinitionId), cts.Token);

        saved.Should().BeEquivalentTo(new MonitoringRule
        {
            Id = saved!.Id,
            OwnerUserId = OwnerId,
            SubjectUserId = Staff.UserId,
            Event = MonitoringEvent.StageFinished,
            MinimumPriority = Priority.High,
            TemplateId = TemplateId,
            StageDefinitionId = ConsultationDefinitionId,
            IsEnabled = true,
            CreatedAtUtc = Now,
            UpdatedAtUtc = Now,
        });
        saved.Id.Should().NotBeEmpty();
        response.Should().Be(new MonitoringRuleResponse(saved.Id, Staff.UserId, Staff.Username, "StageFinished", "High",
            TemplateId, TemplateName, ConsultationDefinitionId, ConsultationStage, null, null, null, true, Now, Now));
        Rules.Verify(r => r.AddAsync(saved, cts.Token), Times.Once);
    }

    [Test]
    public async Task Handle_NoConditions_NamesNothing()
    {
        var response = await Handler.Handle(new CreateMonitoringRuleCommand(OwnerId, null, "PathwayCreated", null, null, null, false), CancellationToken.None);

        response.Should().BeEquivalentTo(new { SubjectUsername = (string?)null, TemplateName = (string?)null, StageName = (string?)null, IsEnabled = false });
        Identity.Verify(i => i.GetUsernamesByIdAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
        Stages.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Handle_AdminHasTheMostRules_ThrowsConflict()
    {
        Rules.Setup(r => r.CountForOwnerAsync(OwnerId, It.IsAny<CancellationToken>())).ReturnsAsync(MonitoringRuleInput.MaxRulesPerAdmin);

        var act = () => Handler.Handle(Command(), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("You have 50 rules*");
        Rules.Verify(r => r.AddAsync(It.IsAny<MonitoringRule>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_UnknownPerson_ThrowsNotFound()
    {
        var act = () => Handler.Handle(Command(subject: Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage("User*");
    }

    [Test]
    public async Task Handle_StageUnlockedForSomeoneNotAnAdmin_ThrowsValidation()
    {
        var act = () => Handler.Handle(new CreateMonitoringRuleCommand(OwnerId, Staff.UserId, "StageUnlocked", null, null, null, true), CancellationToken.None);

        (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Should().ContainSingle()
            .Which.PropertyName.Should().Be("SubjectUserId");
        Rules.Verify(r => r.AddAsync(It.IsAny<MonitoringRule>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_StageUnlockedForAnAdminOrAnyone_Adds()
    {
        await Handler.Handle(new CreateMonitoringRuleCommand(OwnerId, Admin.UserId, "StageUnlocked", null, null, null, true), CancellationToken.None);
        await Handler.Handle(new CreateMonitoringRuleCommand(OwnerId, null, "StageUnlocked", null, null, null, true), CancellationToken.None);

        Rules.Verify(r => r.AddAsync(It.IsAny<MonitoringRule>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Test]
    public async Task Handle_UnknownTemplate_ThrowsNotFound()
    {
        var act = () => Handler.Handle(Command(template: Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage("Template*");
    }

    [Test]
    public async Task Handle_UnknownStage_ThrowsNotFound()
    {
        var act = () => Handler.Handle(Command(stage: Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage("Stage*");
        Rules.Verify(r => r.AddAsync(It.IsAny<MonitoringRule>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
