using MedApp.Application.Activity.Queries.SearchActivity;
using MedApp.Contracts.Activity.Requests;
using MedApp.Contracts.Activity.Responses;
using MedApp.Contracts.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedApp.API.Controllers;

/// <summary>
/// The activity log (admins only): who viewed, searched or changed patient data, signed in or out, or changed an
/// account. A patient's page shows the same log filtered to them, as its access report.
/// </summary>
[ApiController]
[Route("api/activity")]
[Tags("Activity Log")]
public sealed class ActivityController(IMediator mediator) : ControllerBase
{
    /// <summary>One page of the activity log, newest first by default.</summary>
    [HttpGet("search")]
    [ProducesResponseType(typeof(PagedResponse<ActivityEntryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SearchActivity([FromQuery] SearchActivityRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new SearchActivityQuery(
            request.Page,
            request.PageSize,
            request.Ascending,
            request.PatientId,
            request.UserId,
            request.Search,
            request.From,
            request.To,
            request.Actions), ct));
}
