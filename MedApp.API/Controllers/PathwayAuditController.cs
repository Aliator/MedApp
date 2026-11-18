using MedApp.Application.Common.Identity;
using MedApp.API.Common.Authentication;
using MedApp.Application.Pathways.Audit.Queries.SearchPathwayAudit;
using MedApp.Contracts.Common;
using MedApp.Contracts.Pathways.Audit.Requests;
using MedApp.Contracts.Pathways.Audit.Responses;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedApp.API.Controllers;

[ApiController]
[Route("api/pathways/audit")]
[Tags("Pathway Audit")]
public sealed class PathwayAuditController(IMediator mediator) : ControllerBase
{
    /// <summary>One page of the pathway audit log, newest first by default.</summary>
    [HttpGet("search")]
    [ProducesResponseType(typeof(PagedResponse<PatientPathwayAuditEntryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [RequirePermission(Permission.ViewAudit)]
    public async Task<IActionResult> SearchPathwayAudit([FromQuery] SearchPathwayAuditRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new SearchPathwayAuditQuery(
            request.Page,
            request.PageSize,
            request.Ascending,
            request.PathwayId,
            request.PatientId,
            request.UserId,
            request.Action,
            request.Search,
            request.From,
            request.To,
            request.Actions), ct));
}
