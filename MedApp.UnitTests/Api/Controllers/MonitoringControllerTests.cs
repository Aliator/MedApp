using FluentAssertions;
using MedApp.API.Controllers;
using MedApp.Application.Monitoring.Commands.CreateMonitoringRule;
using MedApp.Application.Monitoring.Commands.DeleteMonitoringRule;
using MedApp.Application.Monitoring.Commands.UpdateMonitoringRule;
using MedApp.Application.Monitoring.Queries.GetMyMonitoringRules;
using MedApp.Contracts.Monitoring.Requests;
using MedApp.Contracts.Monitoring.Responses;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static MedApp.UnitTests.Common.Constants.ApiTestConstants;

namespace MedApp.UnitTests.Api.Controllers;

[TestFixture]
public sealed class MonitoringControllerTests
{
    private static readonly Guid SubjectId = Guid.Parse("0000000c-0000-0000-0000-000000000011");
    private static readonly Guid TemplateId = Guid.Parse("0000000c-0000-0000-0000-000000000012");
    private static readonly Guid StageId = Guid.Parse("0000000c-0000-0000-0000-000000000013");

    private static readonly SaveMonitoringRuleRequest Request = new()
    {
        SubjectUserId = SubjectId,
        Event = "StageFinished",
        MinimumPriority = "High",
        TemplateId = TemplateId,
        StageDefinitionId = StageId,
        IsEnabled = false,
    };

    private static readonly MonitoringRuleResponse Rule = new(
        RecordId, SubjectId, "test.staff", "StageFinished", "High", TemplateId, "Template", StageId, "Stage", false, DateTime.UnixEpoch, DateTime.UnixEpoch);

    private static MonitoringController Controller(RecordingMediator mediator) =>
        new MonitoringController(mediator).WithRequest(SignedIn("Admin"));

    [Test]
    public async Task GetMine_ReturnsTheSignedInAdminsRules()
    {
        IReadOnlyList<MonitoringRuleResponse> rules = [Rule];
        var mediator = new RecordingMediator(_ => rules);
        using var cts = new CancellationTokenSource();

        var result = await Controller(mediator).GetMine(cts.Token);

        mediator.Request.Should().Be(new GetMyMonitoringRulesQuery(UserId));
        mediator.Tokens.Single().Should().Be(cts.Token);
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(rules);
    }

    [Test]
    public async Task Create_AddsTheRuleForTheSignedInAdmin_Returns201WithIt()
    {
        var mediator = new RecordingMediator(_ => Rule);

        var result = await Controller(mediator).Create(Request, CancellationToken.None);

        mediator.Request.Should().Be(new CreateMonitoringRuleCommand(UserId, SubjectId, "StageFinished", "High", TemplateId, StageId, false));
        var created = result.Should().BeOfType<CreatedResult>().Subject;
        created.Location.Should().Be($"/api/monitoring/rules/{RecordId}");
        created.Value.Should().Be(Rule);
    }

    [Test]
    public async Task Update_ReplacesTheSignedInAdminsRule()
    {
        var mediator = new RecordingMediator(_ => Rule);

        var result = await Controller(mediator).Update(RecordId, Request, CancellationToken.None);

        mediator.Request.Should().Be(new UpdateMonitoringRuleCommand(RecordId, UserId, SubjectId, "StageFinished", "High", TemplateId, StageId, false));
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(Rule);
    }

    [Test]
    public async Task Delete_DeletesTheSignedInAdminsRule()
    {
        var mediator = new RecordingMediator();

        var result = await Controller(mediator).Delete(RecordId, CancellationToken.None);

        mediator.Request.Should().Be(new DeleteMonitoringRuleCommand(RecordId, UserId));
        result.Should().BeOfType<NoContentResult>();
    }

    [Test]
    public void Controller_IsForAdminsOnly()
    {
        // The API has no fallback policy: without this attribute, any staff member could watch the others.
        typeof(MonitoringController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>()
            .Should().ContainSingle().Which.Roles.Should().Be("Admin");
        typeof(MonitoringController).GetMethods()
            .Should().NotContain(m => m.GetCustomAttributes(typeof(AllowAnonymousAttribute), true).Length > 0);
    }

    [Test]
    public void Request_IsOnByDefault()
    {
        new SaveMonitoringRuleRequest().IsEnabled.Should().BeTrue();
    }
}
