using MedApp.Application.Procedures.Insights.Queries.GetProcedureInsights;
using MedApp.Contracts.Procedures.Insights.Requests;
using MedApp.Contracts.Procedures.Insights.Responses;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedApp.API.Controllers;

[ApiController]
[Route("api/procedures/insights")]
[Tags("Procedure Insights")]
public sealed class ProcedureInsightsController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// An overview of procedure work over the last few UK weeks: open, in progress, overdue and completed counts against
    /// last week, overdue work by assignee and priority, average time at each stage, and weekly throughput.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ProcedureInsightsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetProcedureInsights([FromQuery] GetProcedureInsightsRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new GetProcedureInsightsQuery(request.Weeks, request.TemplateId), ct));
}
