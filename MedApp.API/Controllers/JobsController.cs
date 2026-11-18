using MedApp.Application.Jobs.Commands.RunScheduledJob;
using MedApp.Application.Jobs.Queries.GetScheduledJobs;
using MedApp.Contracts.Jobs.Responses;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedApp.API.Controllers;

/// <summary>
/// The scheduled jobs (admins; <c>ManageSetup</c> once permissions exist): how each last ran, and Run now. Jobs run
/// inside the API, one copy at a time each (a lease in SQL Server). Nothing here reads patient data.
/// </summary>
[ApiController]
[Route("api/jobs")]
[Tags("Jobs")]
[Authorize(Roles = "Admin")]
public sealed class JobsController(IMediator mediator) : ControllerBase
{
    /// <summary>Every scheduled job, by title, with its last run and its next.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ScheduledJobResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetJobs(CancellationToken ct) =>
        Ok(await mediator.Send(new GetScheduledJobsQuery(), ct));

    /// <summary>
    /// Runs a job now, in this request, and returns it afterwards (a failed run is a 200 that shows the failure). A 409
    /// when another copy is running it or it's switched off; a 404 for an unknown job.
    /// </summary>
    [HttpPost("{name}/run")]
    [ProducesResponseType(typeof(ScheduledJobResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RunJob(string name, CancellationToken ct) =>
        Ok(await mediator.Send(new RunScheduledJobCommand(name), ct));
}
