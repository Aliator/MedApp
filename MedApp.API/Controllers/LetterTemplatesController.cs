using MedApp.API.Attachments;
using MedApp.API.Common.Authentication;
using MedApp.Application.Attachments.Common;
using MedApp.Application.Common.Identity;
using MedApp.Application.Letters.Commands.CreateLetterTemplate;
using MedApp.Application.Letters.Commands.DeleteLetterTemplate;
using MedApp.Application.Letters.Commands.SetLetterTemplateArchived;
using MedApp.Application.Letters.Commands.UpdateLetterTemplate;
using MedApp.Application.Letters.Queries.GetLetterMergeFields;
using MedApp.Application.Letters.Queries.GetLetterTemplateById;
using MedApp.Application.Letters.Queries.GetLetterTemplates;
using MedApp.Application.Letters.Queries.PreviewLetterTemplate;
using MedApp.Contracts.Letters.Requests;
using MedApp.Contracts.Letters.Responses;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace MedApp.API.Controllers;

/// <summary>
/// Letter templates (decision 99 of docs/centre-phases-3-5-plan.md): what letters start from. Everyone signed in reads
/// them (UseApp), as letter writers will choose one; holders of ManageSetup keep them. Every change carries the version
/// the caller saw (<c>expectedVersion</c>): 409 if someone has changed it since. One a letter was written from is archived, never deleted; an unused one can be deleted.
/// Templates hold no patient data, so reading them records nothing in the activity log.
/// </summary>
[ApiController]
[Route("api/letter-templates")]
[Tags("Letter templates")]
public sealed class LetterTemplatesController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// The largest body a template's change or preview may send: room for its 22,200 characters of text, JSON-escaped
    /// and in UTF-8, and no more (a larger one is a 413 before it's read).
    /// </summary>
    public const int MaxRequestBytes = 256 * 1024;

    [HttpGet]
    [RequirePermission(Permission.UseApp)]
    [ProducesResponseType(typeof(IReadOnlyList<LetterTemplateResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLetterTemplates([FromQuery] bool includeArchived, CancellationToken ct) =>
        Ok(await mediator.Send(new GetLetterTemplatesQuery(includeArchived), ct));

    [HttpGet("{id:guid}")]
    [RequirePermission(Permission.UseApp)]
    [ProducesResponseType(typeof(LetterTemplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLetterTemplate(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new GetLetterTemplateByIdQuery(id), ct));

    /// <summary>The merge fields a template's text may hold, with what fills each and the example a preview uses.</summary>
    [HttpGet("merge-fields")]
    [RequirePermission(Permission.UseApp)]
    [ProducesResponseType(typeof(IReadOnlyList<LetterMergeFieldResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMergeFields(CancellationToken ct) =>
        Ok(await mediator.Send(new GetLetterMergeFieldsQuery(), ct));

    [HttpPost]
    [RequirePermission(Permission.ManageSetup)]
    [RequestSizeLimit(MaxRequestBytes)]
    [ProducesResponseType(typeof(LetterTemplateResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateLetterTemplate([FromBody] CreateLetterTemplateRequest request, CancellationToken ct)
    {
        var template = await mediator.Send(new CreateLetterTemplateCommand(
            request.Name,
            request.Kind,
            request.TeamId,
            request.Letterhead,
            request.BodySkeleton,
            request.Closing), ct);
        return Created($"/api/letter-templates/{template.Id}", template);
    }

    /// <summary>Replaces a template's details; letters already started from it keep their copy.</summary>
    [HttpPut("{id:guid}")]
    [RequirePermission(Permission.ManageSetup)]
    [RequestSizeLimit(MaxRequestBytes)]
    [ProducesResponseType(typeof(LetterTemplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateLetterTemplate(
        Guid id, [FromBody] UpdateLetterTemplateRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new UpdateLetterTemplateCommand(
            id,
            request.Name,
            request.Kind,
            request.TeamId,
            request.Letterhead,
            request.BodySkeleton,
            request.Closing,
            request.ExpectedVersion), ct));

    /// <summary>Archives a template: it can't be chosen for new letters.</summary>
    [HttpPost("{id:guid}/archive")]
    [RequirePermission(Permission.ManageSetup)]
    [ProducesResponseType(typeof(LetterTemplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ArchiveLetterTemplate(
        Guid id, [FromBody] LetterTemplateVersionRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new SetLetterTemplateArchivedCommand(id, true, request.ExpectedVersion), ct));

    [HttpPost("{id:guid}/restore")]
    [RequirePermission(Permission.ManageSetup)]
    [ProducesResponseType(typeof(LetterTemplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RestoreLetterTemplate(
        Guid id, [FromBody] LetterTemplateVersionRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new SetLetterTemplateArchivedCommand(id, false, request.ExpectedVersion), ct));

    /// <summary>Deletes a template no letter was written from (409 for one that has letters: archive it instead).</summary>
    [HttpDelete("{id:guid}")]
    [RequirePermission(Permission.ManageSetup)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteLetterTemplate(Guid id, CancellationToken ct)
    {
        await mediator.Send(new DeleteLetterTemplateCommand(id), ct);
        return NoContent();
    }

    /// <summary>
    /// A template's text, saved or not, as a PDF filled with made-up details and marked as a preview on every page. Sent
    /// as a download, like every file (<see cref="AttachmentResponseHeaders"/>), never shown on the app's origin.
    /// </summary>
    [HttpPost("preview")]
    [RequirePermission(Permission.ManageSetup)]
    [RequestSizeLimit(MaxRequestBytes)]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK, LetterPdf.ContentType)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PreviewLetterTemplate(
        [FromBody] PreviewLetterTemplateRequest request, CancellationToken ct)
    {
        var pdf = await mediator.Send(new PreviewLetterTemplateQuery(
            request.Name,
            request.Letterhead,
            request.BodySkeleton,
            request.Closing,
            request.LargePrint), ct);

        var download = new AttachmentDownload(pdf.FileName, LetterPdf.ContentType, pdf.Content.Length, new MemoryStream(pdf.Content));
        AttachmentResponseHeaders.Apply(Response, download);
        return new FileStreamResult(download.Content, download.ContentType);
    }
}
