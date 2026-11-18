using MedApp.API.Common.Authentication;
using MedApp.Application.Common.Identity;
using MedApp.Application.Results.Commands.MarkResultEnteredInError;
using MedApp.Application.Results.Commands.MarkResultSeen;
using MedApp.Application.Results.Commands.ReassignResult;
using MedApp.Application.Results.Commands.RecordExternalResult;
using MedApp.Application.Results.Commands.SignOffResult;
using MedApp.Application.Results.Queries.GetResult;
using MedApp.Application.Results.Queries.SearchResults;
using MedApp.Contracts.Common;
using MedApp.Contracts.Results.Requests;
using MedApp.Contracts.Results.Responses;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace MedApp.API.Controllers;

/// <summary>
/// Results (<c>docs/centre-phases-3-5-plan.md</c>, section 2.3): a report a named clinician must sign off, made when a
/// specimen's report is attached or from an attached file marked as a result from elsewhere. Holders of SignOffResults
/// mark them seen, sign them off with what was done, and take them over or reassign them; anyone who can see patients
/// reads them. Every change carries the version the caller saw (<c>expectedVersion</c>) and is audited. <b>MedApp never
/// reads, interprets or flags a result's contents</b>: the report is the file, as it came.
/// </summary>
[ApiController]
[Route("api/results")]
[Tags("Results")]
public sealed class ResultsController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// One page of results: awaiting the caller's sign-off, everyone's, needing a clinician (oldest report first),
    /// signed off (newest first), or all of a pathway's or patient's. Each patient shown is recorded as listed.
    /// </summary>
    [HttpGet("search")]
    [RequirePermission(Permission.ViewPatients)]
    [ProducesResponseType(typeof(PagedResponse<ResultSummaryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SearchResults([FromQuery] SearchResultsRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new SearchResultsQuery(
            request.Page, request.PageSize, request.View, request.SpecialtyId, request.PathwayId, request.PatientId,
            User.ToPathwayActor().UserId, request.Search, request.Sort, request.Ascending), ct));

    /// <summary>How many results await the caller's sign-off, for the side nav. Names no patient, so records nothing.</summary>
    [HttpGet("count")]
    [RequirePermission(Permission.ViewPatients)]
    [ProducesResponseType(typeof(ResultCountResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> CountResults(CancellationToken ct) =>
        Ok(await mediator.Send(new CountResultsQuery(User.ToPathwayActor().UserId), ct));

    /// <summary>One result. Opening it is recorded (<c>ResultViewed</c>).</summary>
    [HttpGet("{id:guid}")]
    [RequirePermission(Permission.ViewPatients)]
    [ProducesResponseType(typeof(ResultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetResult(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new GetResultQuery(id, User.ToPathwayActor()), ct));

    /// <summary>Marks an attached file as a result from elsewhere, for a named clinician to sign off.</summary>
    [HttpPost("external")]
    [RequirePermission(Permission.WorkPathways)]
    [ProducesResponseType(typeof(ResultResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RecordExternalResult([FromBody] RecordExternalResultRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new RecordExternalResultCommand(
            request.AttachmentId, request.Title, request.PathwayId, request.ResponsibleUserId, User.ToPathwayActor()), ct);
        return Created($"/api/results/{result.Id}", result);
    }

    /// <summary>Marks a result seen: read, and action under way.</summary>
    [HttpPost("{id:guid}/seen")]
    [RequirePermission(Permission.SignOffResults)]
    [ProducesResponseType(typeof(ResultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> MarkSeen(Guid id, [FromBody] ResultVersionRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new MarkResultSeenCommand(id, request.ExpectedVersion, User.ToPathwayActor()), ct));

    /// <summary>
    /// Signs a result off with what was done, chosen by the clinician (and a detail, required for Other). Final. If the
    /// responsible clinician didn't sign it off, they're told.
    /// </summary>
    [HttpPost("{id:guid}/sign-off")]
    [RequirePermission(Permission.SignOffResults)]
    [ProducesResponseType(typeof(ResultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SignOff(Guid id, [FromBody] SignOffResultRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new SignOffResultCommand(
            id, request.Action, request.Detail, request.ExpectedVersion, User.ToPathwayActor()), ct));

    /// <summary>Gives a result to another responsible clinician, or takes it over; the new one is told.</summary>
    [HttpPost("{id:guid}/reassign")]
    [RequirePermission(Permission.SignOffResults)]
    [ProducesResponseType(typeof(ResultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reassign(Guid id, [FromBody] ReassignResultRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new ReassignResultCommand(
            id, request.ResponsibleUserId, request.ExpectedVersion, User.ToPathwayActor()), ct));

    /// <summary>
    /// Marks a result entered in error (a report on the wrong patient or specimen): a holder of CorrectRecords, or
    /// whoever reported it within 24 hours (checked by the handler: 403 otherwise). Its file is deleted, and a
    /// specimen's goes back to the lab to be reported again.
    /// </summary>
    [HttpPost("{id:guid}/entered-in-error")]
    [RequirePermission(Permission.ViewPatients)]
    [ProducesResponseType(typeof(ResultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> MarkEnteredInError(Guid id, [FromBody] MarkResultEnteredInErrorRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new MarkResultEnteredInErrorCommand(
            id, request.Detail, request.ExpectedVersion, User.ToPathwayActor()), ct));
}
