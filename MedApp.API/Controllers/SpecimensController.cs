using MedApp.API.Attachments;
using MedApp.API.Common.Authentication;
using MedApp.Application.Attachments.Common;
using MedApp.Application.Common.Identity;
using MedApp.Application.Specimens.Commands.EditSpecimen;
using MedApp.Application.Specimens.Commands.MarkSpecimenEnteredInError;
using MedApp.Application.Specimens.Commands.MarkSpecimenLost;
using MedApp.Application.Specimens.Commands.MarkSpecimenReceived;
using MedApp.Application.Specimens.Commands.MarkSpecimenSent;
using MedApp.Application.Specimens.Commands.RecordSpecimen;
using MedApp.Application.Specimens.Commands.RejectSpecimen;
using MedApp.Application.Specimens.Commands.ReportSpecimen;
using MedApp.Application.Specimens.Queries.GetSpecimen;
using MedApp.Application.Specimens.Queries.SearchSpecimens;
using MedApp.Contracts.Common;
using MedApp.Contracts.Specimens.Requests;
using MedApp.Contracts.Specimens.Responses;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace MedApp.API.Controllers;

/// <summary>
/// Specimens (<c>docs/centre-phases-3-5-plan.md</c>, section 2.2): taken at a pathway's stage, tracked to the lab and
/// back by their own number, until the lab's report (a file) is attached. Holders of RecordSpecimens take, send and
/// edit them; holders of ProcessSpecimens receive, reject and report them; either can mark one lost. Anyone who can see
/// patients reads them. Every change carries the version the caller saw (<c>expectedVersion</c>) and is audited.
/// MedApp tracks and shows specimens; it never reads or interprets a report.
/// </summary>
[ApiController]
[Route("api")]
[Tags("Specimens")]
public sealed class SpecimensController(IMediator mediator) : ControllerBase
{
    private const string UploadContentType = "application/octet-stream";

    /// <summary>Records a specimen taken at one of the pathway's stages (the current one, or one an admin unlocked).</summary>
    [HttpPost("pathways/{id:guid}/specimens")]
    [RequirePermission(Permission.RecordSpecimens)]
    [ProducesResponseType(typeof(SpecimenResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RecordSpecimen(Guid id, [FromBody] RecordSpecimenRequest request, CancellationToken ct)
    {
        var specimen = await mediator.Send(new RecordSpecimenCommand(
            id,
            request.StageId,
            request.SpecimenTypeId,
            request.LaboratoryId,
            request.TakenAtUtc,
            request.Site,
            request.RequestNotes,
            request.RequestingUserId,
            User.ToPathwayActor()), ct);

        return Created($"/api/specimens/{specimen.Id}", specimen);
    }

    /// <summary>
    /// One page of specimens: a lab worklist view (oldest first), or all of a pathway's or patient's (newest first).
    /// Each patient shown is recorded as listed.
    /// </summary>
    [HttpGet("specimens/search")]
    [RequirePermission(Permission.ViewPatients)]
    [ProducesResponseType(typeof(PagedResponse<SpecimenSummaryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SearchSpecimens([FromQuery] SearchSpecimensRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new SearchSpecimensQuery(
            request.Page, request.PageSize, request.View, request.LaboratoryId, request.PathwayId, request.PatientId), ct));

    /// <summary>
    /// The specimen with this number, typed or scanned from its label. The number is in the body, never the URL, so it
    /// stays out of request logs. Recorded as viewing it (<c>SpecimenViewed</c>); a 404 when no specimen has it.
    /// </summary>
    [HttpPost("specimens/find")]
    [RequirePermission(Permission.ViewPatients)]
    [ProducesResponseType(typeof(SpecimenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> FindSpecimen([FromBody] FindSpecimenRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new FindSpecimenQuery(request.Number, User.ToPathwayActor()), ct));

    /// <summary>
    /// One specimen, with what its label and request sheet print. Opening it, or printing either, is recorded
    /// (<c>SpecimenViewed</c>).
    /// </summary>
    [HttpGet("specimens/{id:guid}")]
    [RequirePermission(Permission.ViewPatients)]
    [ProducesResponseType(typeof(SpecimenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSpecimen(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new GetSpecimenQuery(id, User.ToPathwayActor()), ct));

    /// <summary>Corrects a specimen's details, before it's sent.</summary>
    [HttpPut("specimens/{id:guid}")]
    [RequirePermission(Permission.RecordSpecimens)]
    [ProducesResponseType(typeof(SpecimenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> EditSpecimen(Guid id, [FromBody] EditSpecimenRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new EditSpecimenCommand(
            id,
            request.SpecimenTypeId,
            request.LaboratoryId,
            request.TakenAtUtc,
            request.Site,
            request.RequestNotes,
            request.RequestingUserId,
            request.ExpectedVersion,
            User.ToPathwayActor()), ct));

    /// <summary>Records a specimen sent to the lab, with the courier's or batch's reference.</summary>
    [HttpPost("specimens/{id:guid}/sent")]
    [RequirePermission(Permission.RecordSpecimens)]
    [ProducesResponseType(typeof(SpecimenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> MarkSent(Guid id, [FromBody] SpecimenStepRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new MarkSpecimenSentCommand(
            id, request.AtUtc, request.Reference, request.ExpectedVersion, User.ToPathwayActor()), ct));

    /// <summary>Records a specimen received by the lab, with the lab's own reference.</summary>
    [HttpPost("specimens/{id:guid}/received")]
    [RequirePermission(Permission.ProcessSpecimens)]
    [ProducesResponseType(typeof(SpecimenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> MarkReceived(Guid id, [FromBody] SpecimenStepRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new MarkSpecimenReceivedCommand(
            id, request.AtUtc, request.Reference, request.ExpectedVersion, User.ToPathwayActor()), ct));

    /// <summary>Records that the lab rejected a specimen, with a reason; whoever asked for it and took it are told.</summary>
    [HttpPost("specimens/{id:guid}/rejected")]
    [RequirePermission(Permission.ProcessSpecimens)]
    [ProducesResponseType(typeof(SpecimenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectSpecimenRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new RejectSpecimenCommand(
            id, request.Reason, request.Detail, request.ExpectedVersion, User.ToPathwayActor()), ct));

    /// <summary>
    /// Marks a specimen lost (holders of RecordSpecimens or ProcessSpecimens, checked by the handler: 403 otherwise);
    /// whoever asked for it and took it are told.
    /// </summary>
    [HttpPost("specimens/{id:guid}/lost")]
    [RequirePermission(Permission.ViewPatients)]
    [ProducesResponseType(typeof(SpecimenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> MarkLost(Guid id, [FromBody] CloseSpecimenRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new MarkSpecimenLostCommand(
            id, request.Detail, request.ExpectedVersion, User.ToPathwayActor()), ct));

    /// <summary>
    /// Marks a specimen entered in error: whoever recorded it, within 24 hours, or a holder of CorrectRecords at any
    /// time (checked by the handler: 403 otherwise).
    /// </summary>
    [HttpPost("specimens/{id:guid}/entered-in-error")]
    [RequirePermission(Permission.ViewPatients)]
    [ProducesResponseType(typeof(SpecimenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> MarkEnteredInError(Guid id, [FromBody] CloseSpecimenRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new MarkSpecimenEnteredInErrorCommand(
            id, request.Detail, request.ExpectedVersion, User.ToPathwayActor()), ct));

    /// <summary>
    /// Attaches the lab's report to a received specimen. The body is the file's bytes (<c>application/octet-stream</c>,
    /// at most 20 MB), its name in the <c>X-File-Name</c> header, percent-encoded, under every rule an attached file
    /// has (its type from its contents). It's attached to the pathway, or the patient if the pathway has closed.
    /// </summary>
    [HttpPost("specimens/{id:guid}/report")]
    [RequestSizeLimit(AttachmentLimits.MaxFileBytes)]
    [RequirePermission(Permission.ProcessSpecimens)]
    [ProducesResponseType(typeof(SpecimenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status415UnsupportedMediaType)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Report(
        Guid id,
        [FromQuery] int expectedVersion,
        [FromHeader(Name = AttachmentResponseHeaders.FileNameHeader)] string? fileName,
        CancellationToken ct)
    {
        if (!MediaTypeHeaderValue.TryParse(Request.ContentType, out var bodyType)
            || !bodyType.MediaType.Equals(UploadContentType, StringComparison.OrdinalIgnoreCase))
            throw new UnsupportedFileTypeException($"Send the file's bytes as {UploadContentType}.");

        return Ok(await mediator.Send(new ReportSpecimenCommand(
            id,
            AttachmentResponseHeaders.DecodeFileName(fileName),
            Request.ContentLength,
            Request.Body,
            expectedVersion,
            User.ToPathwayActor()), ct));
    }
}
