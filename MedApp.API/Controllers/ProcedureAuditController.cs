using MedApp.Application.Procedures.Audit.Queries.SearchProcedureAudit;
using MedApp.Contracts.Common;
using MedApp.Contracts.Procedures.Audit.Requests;
using MedApp.Contracts.Procedures.Audit.Responses;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedApp.API.Controllers;

[ApiController]
[Route("api/procedures/audit")]
[Tags("Procedure Audit")]
public sealed class ProcedureAuditController(IMediator mediator) : ControllerBase
{
    /// <summary>One page of the procedure audit log, newest first by default.</summary>
    [HttpGet("search")]
    [ProducesResponseType(typeof(PagedResponse<PatientProcedureAuditEntryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SearchProcedureAudit([FromQuery] SearchProcedureAuditRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new SearchProcedureAuditQuery(
            request.Page,
            request.PageSize,
            request.Ascending,
            request.ProcedureId,
            request.PatientId,
            request.UserId,
            request.Action,
            request.Search,
            request.From,
            request.To), ct));
}
