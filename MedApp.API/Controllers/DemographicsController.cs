using MedApp.API.Common.Authentication;
using MedApp.Application.Demographics.Queries.GetDemographicsStatus;
using MedApp.Application.Demographics.Queries.LookUpDemographics;
using MedApp.Contracts.Demographics.Requests;
using MedApp.Contracts.Demographics.Responses;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedApp.API.Controllers;

/// <summary>
/// Looking someone up by NHS number in a demographics service (PDS), to fill in a patient's form. Off unless
/// configured (<c>Pds:Mode</c>); only an offline stub of synthetic 999 numbers exists.
/// </summary>
[ApiController]
[Route("api/demographics")]
[Authorize(Policy = AuthPolicies.Staff)]
public sealed class DemographicsController(IMediator mediator) : ControllerBase
{
    /// <summary>Whether a lookup is set up, so the forms know whether to offer one.</summary>
    [HttpGet("status")]
    [ProducesResponseType(typeof(DemographicsStatusResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatus(CancellationToken ct) =>
        Ok(await mediator.Send(new GetDemographicsStatusQuery(), ct));

    /// <summary>
    /// The person with this NHS number (spaces allowed). 400 for a number that isn't valid; 404 when nobody has it, or
    /// no lookup is set up. Recorded in the activity log, without the number. Never cached. A POST, so the number is
    /// in the body, never in a URL that logs and traces record.
    /// </summary>
    [HttpPost("lookup")]
    [ProducesResponseType(typeof(DemographicsLookupResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> LookUp([FromBody] LookUpDemographicsRequest request, CancellationToken ct)
    {
        var person = await mediator.Send(new LookUpDemographicsQuery(request.NhsNumber ?? string.Empty), ct);
        Response.Headers.CacheControl = "no-store";
        return Ok(person);
    }
}
