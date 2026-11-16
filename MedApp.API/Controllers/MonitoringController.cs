using MedApp.API.Common.Authentication;
using MedApp.Application.Monitoring.Commands.CreateMonitoringRule;
using MedApp.Application.Monitoring.Commands.DeleteMonitoringRule;
using MedApp.Application.Monitoring.Commands.UpdateMonitoringRule;
using MedApp.Application.Monitoring.Queries.GetMyMonitoringRules;
using MedApp.Contracts.Monitoring.Requests;
using MedApp.Contracts.Monitoring.Responses;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedApp.API.Controllers;

/// <summary>
/// The signed-in admin's monitoring rules: what staff do to procedures that they want to be told about. Each admin
/// sees and changes only their own; another admin's rule is a 404.
/// </summary>
[ApiController]
[Route("api/monitoring/rules")]
[Authorize(Roles = "Admin")]
[Tags("Monitoring")]
public sealed class MonitoringController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<MonitoringRuleResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMine(CancellationToken ct) =>
        Ok(await mediator.Send(new GetMyMonitoringRulesQuery(User.GetUserId()), ct));

    [HttpPost]
    [ProducesResponseType(typeof(MonitoringRuleResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] SaveMonitoringRuleRequest request, CancellationToken ct)
    {
        var rule = await mediator.Send(new CreateMonitoringRuleCommand(
            User.GetUserId(),
            request.SubjectUserId,
            request.Event,
            request.MinimumPriority,
            request.TemplateId,
            request.StageDefinitionId,
            request.IsEnabled), ct);

        return Created($"/api/monitoring/rules/{rule.Id}", rule);
    }

    /// <summary>Replaces one of your rules, including switching it on or off.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(MonitoringRuleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] SaveMonitoringRuleRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new UpdateMonitoringRuleCommand(
            id,
            User.GetUserId(),
            request.SubjectUserId,
            request.Event,
            request.MinimumPriority,
            request.TemplateId,
            request.StageDefinitionId,
            request.IsEnabled), ct));

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await mediator.Send(new DeleteMonitoringRuleCommand(id, User.GetUserId()), ct);
        return NoContent();
    }
}
