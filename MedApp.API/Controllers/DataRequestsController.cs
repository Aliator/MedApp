using MedApp.API.Attachments;
using MedApp.API.Common.Authentication;
using MedApp.Application.Attachments.Common;
using MedApp.Application.Common.Identity;
using MedApp.Application.DataRequests.Commands;
using MedApp.Application.DataRequests.Queries;
using MedApp.Contracts.Common;
using MedApp.Contracts.DataRequests;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace MedApp.API.Controllers;

/// <summary>
/// Patients' data requests (subject access, UK GDPR Article 15; <c>docs/centre-phases-3-5-plan.md</c>, decisions 160 to
/// 163): logged by a holder of ManagePrivacy with the identity check and the one-month due date, reviewed by a clinician
/// (SignOffResults) who withholds what meets the serious harm test, and answered with a pack (a plain-language PDF, the
/// record as JSON and the files) built on request and downloaded, never stored. Every change carries the version the
/// caller saw (<c>expectedVersion</c>) and is recorded in the activity log, as are reads.
/// </summary>
[ApiController]
[Route("api/data-requests")]
public sealed class DataRequestsController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Data requests, open ones first by due date. For holders of ManagePrivacy, and of SignOffResults (who review them);
    /// the handler checks. Records each patient listed.
    /// </summary>
    [HttpGet("search")]
    [RequirePermission(Permission.ViewPatients)]
    [ProducesResponseType(typeof(PagedResponse<DataRequestSummaryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SearchDataRequests([FromQuery] SearchDataRequestsRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new SearchDataRequestsQuery(
            request.Page, request.PageSize, request.Status, request.PatientId, request.AwaitingReview,
            User.ToPathwayActor(), request.Search, request.Sort, request.Ascending), ct));

    /// <summary>Logs a data request as received; its due date is one calendar month on.</summary>
    [HttpPost]
    [RequirePermission(Permission.ManagePrivacy)]
    [ProducesResponseType(typeof(DataRequestResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateDataRequest([FromBody] CreateDataRequestRequest request, CancellationToken ct)
    {
        var created = await mediator.Send(new CreateDataRequestCommand(
            request.PatientId, request.ReceivedOn, request.RequestedByPatient, request.RequesterName,
            request.RequesterRelationship, User.ToPathwayActor()), ct);
        return Created($"/api/data-requests/{created.Id}", created);
    }

    /// <summary>One data request (ManagePrivacy or SignOffResults; the handler checks). Recorded as viewing the patient.</summary>
    [HttpGet("{id:guid}")]
    [RequirePermission(Permission.ViewPatients)]
    [ProducesResponseType(typeof(DataRequestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDataRequest(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new GetDataRequestQuery(id, User.ToPathwayActor()), ct));

    /// <summary>
    /// The items of the record a clinician may withhold (notes, files, letters, results, MDT items, referrals), with their
    /// text, for the review (ManagePrivacy or SignOffResults; the handler checks). Recorded as viewing the patient.
    /// </summary>
    [HttpGet("{id:guid}/review-items")]
    [RequirePermission(Permission.ViewPatients)]
    [ProducesResponseType(typeof(IReadOnlyList<DataRequestReviewItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDataRequestReviewItems(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new GetDataRequestReviewItemsQuery(id, User.ToPathwayActor()), ct));

    /// <summary>Records the clinician review: what's withheld under the serious harm test, each with why.</summary>
    [HttpPut("{id:guid}/review")]
    [RequirePermission(Permission.SignOffResults)]
    [ProducesResponseType(typeof(DataRequestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RecordDataRequestReview(
        Guid id, [FromBody] RecordDataRequestReviewRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new RecordDataRequestReviewCommand(
            id,
            request.Withheld.Select(w => new WithheldItemInput(w.Kind, w.ItemId, w.Reason)).ToList(),
            request.ExpectedVersion,
            User.ToPathwayActor()), ct));

    /// <summary>Records the requester's identity check (needed before the pack can be made).</summary>
    [HttpPost("{id:guid}/identity-check")]
    [RequirePermission(Permission.ManagePrivacy)]
    [ProducesResponseType(typeof(DataRequestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RecordIdentityCheck(
        Guid id, [FromBody] RecordIdentityCheckRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new RecordIdentityCheckCommand(
            id, request.CheckedOn, request.Method, request.Detail, request.ExpectedVersion, User.ToPathwayActor()), ct));

    /// <summary>Extends the due date by up to two more months, with the reason (only within the first month).</summary>
    [HttpPost("{id:guid}/extension")]
    [RequirePermission(Permission.ManagePrivacy)]
    [ProducesResponseType(typeof(DataRequestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ExtendDataRequest(
        Guid id, [FromBody] ExtendDataRequestRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new ExtendDataRequestCommand(
            id, request.Months, request.Reason, request.ExpectedVersion, User.ToPathwayActor()), ct));

    /// <summary>Records that the response was sent, when and how.</summary>
    [HttpPost("{id:guid}/sent")]
    [RequirePermission(Permission.ManagePrivacy)]
    [ProducesResponseType(typeof(DataRequestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RecordDataRequestSent(
        Guid id, [FromBody] RecordDataRequestSentRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new RecordDataRequestSentCommand(
            id, request.SentOn, request.Method, request.ExpectedVersion, User.ToPathwayActor()), ct));

    /// <summary>Refuses the request, with the reason given to the requester.</summary>
    [HttpPost("{id:guid}/refuse")]
    [RequirePermission(Permission.ManagePrivacy)]
    [ProducesResponseType(typeof(DataRequestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RefuseDataRequest(
        Guid id, [FromBody] RefuseDataRequestRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new RefuseDataRequestCommand(
            id, request.Reason, request.ExpectedVersion, User.ToPathwayActor()), ct));

    /// <summary>Records that the requester withdrew the request.</summary>
    [HttpPost("{id:guid}/withdraw")]
    [RequirePermission(Permission.ManagePrivacy)]
    [ProducesResponseType(typeof(DataRequestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> WithdrawDataRequest(
        Guid id, [FromBody] WithdrawDataRequestRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new WithdrawDataRequestCommand(id, request.ExpectedVersion, User.ToPathwayActor()), ct));

    /// <summary>
    /// The response pack as a ZIP download (the report as a PDF, the record as JSON, and the files, leaving out what the
    /// review withheld): built now, never stored, sent <c>no-store</c>. Needs the identity check and the review; recorded
    /// as <c>DataRequestPackDownloaded</c>.
    /// </summary>
    [HttpGet("{id:guid}/pack")]
    [RequirePermission(Permission.ManagePrivacy)]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK, DataRequestPackFile.ContentType)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DownloadDataRequestPack(Guid id, CancellationToken ct)
    {
        var pack = await mediator.Send(new GetDataRequestPackQuery(id, User.ToPathwayActor()), ct);
        var download = new AttachmentDownload(pack.FileName, DataRequestPackFile.ContentType, pack.Content.Length, pack.Content);
        AttachmentResponseHeaders.Apply(Response, download);
        return new FileStreamResult(download.Content, download.ContentType);
    }
}
