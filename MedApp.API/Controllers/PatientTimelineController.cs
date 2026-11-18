using MedApp.API.Common.Authentication;
using MedApp.Application.Pathways.Timeline.Queries.GetPatientTimeline;
using MedApp.Application.Pathways.Timeline.Queries.GetPatientTimelineOverview;
using MedApp.Contracts.Pathways.Timeline.Requests;
using MedApp.Contracts.Pathways.Timeline.Responses;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedApp.API.Controllers;

/// <summary>
/// A patient's timeline: everything that happened to their pathways and appointments, from the pathway audit log
/// and appointments' history. Staff see it for pathways that still exist, without the free text the logs keep;
/// admins see all of it (see <see cref="GetPatientTimelineQuery"/>).
/// </summary>
[ApiController]
[Route("api/patients/{id:guid}/timeline")]
[Tags("Patient Timeline")]
[Authorize(Policy = AuthPolicies.Staff)]
public sealed class PatientTimelineController(IMediator mediator) : ControllerBase
{
    /// <summary>One page of the patient's timeline, newest first; pass the page's <c>NextCursor</c> as <c>Before</c> for the next.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PatientTimelinePageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTimeline(Guid id, [FromQuery] GetPatientTimelineRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new GetPatientTimelineQuery(
            id,
            request.Before,
            request.PageSize,
            request.Kinds,
            request.PathwayIds,
            IsAdmin), ct));

    /// <summary>The patient's pathways that are due, and the pathways to filter the timeline by.</summary>
    [HttpGet("overview")]
    [ProducesResponseType(typeof(PatientTimelineOverviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOverview(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new GetPatientTimelineOverviewQuery(id, IsAdmin), ct));

    private bool IsAdmin => User.IsInRole("Admin");
}
