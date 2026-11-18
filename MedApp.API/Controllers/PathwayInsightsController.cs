using MedApp.Application.Common.Identity;
using MedApp.API.Common.Authentication;
using MedApp.Application.Pathways.Insights.Queries.GetPathwayInsights;
using MedApp.Contracts.Pathways.Insights.Requests;
using MedApp.Contracts.Pathways.Insights.Responses;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedApp.API.Controllers;

[ApiController]
[Route("api/pathways/insights")]
[Tags("Pathway Insights")]
public sealed class PathwayInsightsController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// An overview of pathway work over the last few UK weeks: open, in progress, overdue and completed counts against
    /// last week, overdue work by assignee and priority, average time at each stage, and weekly throughput.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PathwayInsightsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [RequirePermission(Permission.ViewInsights)]
    public async Task<IActionResult> GetPathwayInsights([FromQuery] GetPathwayInsightsRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new GetPathwayInsightsQuery(request.Weeks, request.TemplateId, request.TeamId), ct));
}
