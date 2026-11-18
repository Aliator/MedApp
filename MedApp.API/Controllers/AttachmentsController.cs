using MedApp.Application.Common.Identity;
using MedApp.API.Attachments;
using MedApp.API.Common.Authentication;
using MedApp.Application.Attachments.Commands.DeleteAttachment;
using MedApp.Application.Attachments.Commands.DownloadAttachment;
using MedApp.Application.Attachments.Commands.UploadAttachment;
using MedApp.Application.Attachments.Common;
using MedApp.Application.Attachments.Queries.GetAttachments;
using MedApp.Application.Attachments.Queries.GetAttachmentThumbnail;
using MedApp.Contracts.Attachments.Requests;
using MedApp.Contracts.Attachments.Responses;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace MedApp.API.Controllers;

/// <summary>
/// Files attached to patients, pathways, stages and referrals. Staff can list, upload and download them; staff delete their
/// own, admins any. Files only ever leave through this API, as downloads (<see cref="AttachmentResponseHeaders"/>).
/// </summary>
[ApiController]
[Route("api/attachments")]
[Tags("Attachments")]
public sealed class AttachmentsController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// The files attached to one patient (everything, anywhere), pathway (its own) or stage, newest first. Admins can
    /// ask for deleted ones too.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(AttachmentListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [RequirePermission(Permission.ViewPatients)]
    public async Task<IActionResult> GetAttachments([FromQuery] GetAttachmentsRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new GetAttachmentsQuery(
            request.PatientId,
            request.PathwayId,
            request.StageId,
            request.ReferralId,
            request.IncludeDeleted,
            User.GetUserId(),
            CanCorrectRecords,
            User.HasPermission(Permission.AttachFiles)), ct));

    /// <summary>
    /// Attaches a file to one patient, pathway or stage. The body is the file's bytes
    /// (<c>application/octet-stream</c>), at most 20 MB; its name is in the <c>X-File-Name</c> header, percent-encoded.
    /// Its type is found from its contents: PDF, PNG, JPEG, HEIC/HEIF, Word (.docx) or plain text (.txt). Any other
    /// request type is a 415: a page on another site can't send this one without asking first (CORS), which nothing
    /// allows.
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(AttachmentLimits.MaxFileBytes)]
    [ProducesResponseType(typeof(AttachmentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status415UnsupportedMediaType)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    [RequirePermission(Permission.AttachFiles)]
    public async Task<IActionResult> Upload(
        [FromQuery] AttachmentTargetRequest target,
        [FromHeader(Name = AttachmentResponseHeaders.FileNameHeader)] string? fileName,
        CancellationToken ct)
    {
        if (!MediaTypeHeaderValue.TryParse(Request.ContentType, out var bodyType)
            || !bodyType.MediaType.Equals(UploadContentType, StringComparison.OrdinalIgnoreCase))
            throw new UnsupportedFileTypeException($"Send the file's bytes as {UploadContentType}.");

        var attachment = await mediator.Send(new UploadAttachmentCommand(
            target.PatientId,
            target.PathwayId,
            target.StageId,
            target.ReferralId,
            AttachmentResponseHeaders.DecodeFileName(fileName),
            Request.ContentLength,
            Request.Body,
            User.ToPathwayActor(),
            CanCorrectRecords), ct);

        return Created($"/api/attachments/{attachment.Id}/content", attachment);
    }

    /// <summary>Downloads a file (audited), always as an attachment with its sniffed type, never shown inline.</summary>
    [HttpGet("{id:guid}/content")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK, "application/octet-stream")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    [RequirePermission(Permission.ViewPatients)]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct) =>
        Send(await mediator.Send(new DownloadAttachmentCommand(id, User.ToPathwayActor()), ct));

    /// <summary>An image's thumbnail (re-encoded when it was uploaded), sent as safely as the file itself.</summary>
    [HttpGet("{id:guid}/thumbnail")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK, "application/octet-stream")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    [RequirePermission(Permission.ViewPatients)]
    public async Task<IActionResult> Thumbnail(Guid id, CancellationToken ct) =>
        Send(await mediator.Send(new GetAttachmentThumbnailQuery(id), ct));

    /// <summary>Deletes (hides) a file, audited: staff their own while it can be changed, admins any.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [RequirePermission(Permission.AttachFiles)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await mediator.Send(new DeleteAttachmentCommand(id, User.ToPathwayActor(), CanCorrectRecords), ct);
        return NoContent();
    }

    private FileStreamResult Send(AttachmentDownload download)
    {
        AttachmentResponseHeaders.Apply(Response, download);
        return new FileStreamResult(download.Content, download.ContentType);
    }

    private const string UploadContentType = "application/octet-stream";

    // Deleting anyone's file at any time, and listing deleted ones: holders of CorrectRecords.
    private bool CanCorrectRecords => User.HasPermission(Permission.CorrectRecords);
}
