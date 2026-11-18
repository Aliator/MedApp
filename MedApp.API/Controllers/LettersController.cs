using MedApp.API.Attachments;
using MedApp.API.Common.Authentication;
using MedApp.Application.Attachments.Common;
using MedApp.Application.Common.Identity;
using MedApp.Application.Letters.Commands.ApproveLetter;
using MedApp.Application.Letters.Commands.CreateLetter;
using MedApp.Application.Letters.Commands.ReturnLetter;
using MedApp.Application.Letters.Commands.SendLetter;
using MedApp.Application.Letters.Commands.SubmitLetter;
using MedApp.Application.Letters.Commands.UpdateLetter;
using MedApp.Application.Letters.Commands.WithdrawLetter;
using MedApp.Application.Letters.Commands.WriteLetterCorrection;
using MedApp.Application.Letters.Common;
using MedApp.Application.Letters.Queries.GetLetter;
using MedApp.Application.Letters.Queries.PreviewLetterTemplate;
using MedApp.Application.Letters.Queries.SearchLetters;
using MedApp.Contracts.Common;
using MedApp.Contracts.Letters.Requests;
using MedApp.Contracts.Letters.Responses;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace MedApp.API.Controllers;

/// <summary>
/// Letters (decisions 100 to 108 of docs/centre-phases-3-5-plan.md): clinical correspondence written by staff from
/// templates, approved by a named clinician, made into a PDF and sent, with a record of each sending. Holders of
/// WriteLetters write and submit them, of ApproveLetters approve or return them, and of SendLetters send them; anyone
/// who can see patients reads them (recorded as <c>LetterViewed</c>). Every change carries the version the caller saw
/// (<c>expectedVersion</c>) and is audited. MedApp fills identity and administrative merge fields only: the body is the
/// author's own.
/// </summary>
[ApiController]
[Route("api/letters")]
[Tags("Letters")]
public sealed class LettersController(IMediator mediator) : ControllerBase
{
    /// <summary>The largest body a draft's save may send: its 22,000 characters of text and ten recipients, JSON-escaped and in UTF-8.</summary>
    public const int MaxRequestBytes = 256 * 1024;

    /// <summary>Starts a letter from a template, about a pathway or a referral.</summary>
    [HttpPost]
    [RequirePermission(Permission.WriteLetters)]
    [ProducesResponseType(typeof(LetterResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateLetter([FromBody] CreateLetterRequest request, CancellationToken ct)
    {
        var letter = await mediator.Send(new CreateLetterCommand(
            request.TemplateId, request.PathwayId, request.ReferralId, User.ToPathwayActor()), ct);
        return Created($"/api/letters/{letter.Id}", letter);
    }

    /// <summary>
    /// One page of letters: a view (MyDrafts, AwaitingMyApproval, ReturnedToMe, ToSend, Sent), or every letter of a
    /// patient, pathway or referral. Each patient shown is recorded as listed.
    /// </summary>
    [HttpGet("search")]
    [RequirePermission(Permission.ViewPatients)]
    [ProducesResponseType(typeof(PagedResponse<LetterSummaryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SearchLetters([FromQuery] SearchLettersRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new SearchLettersQuery(
            request.Page, request.PageSize, request.View, request.PatientId, request.PathwayId, request.ReferralId,
            User.GetUserId(), request.Search, request.Sort, request.Ascending), ct));

    /// <summary>The side nav's count: your letters to approve, and every approved letter still to send. Names nobody, so records nothing.</summary>
    [HttpGet("count")]
    [RequirePermission(Permission.ViewPatients)]
    [ProducesResponseType(typeof(LetterCountResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> CountLetters(CancellationToken ct) =>
        Ok(await mediator.Send(new CountLettersQuery(User.GetUserId()), ct));

    /// <summary>One letter, with its text, recipients and what you may do. Recorded as viewing it.</summary>
    [HttpGet("{id:guid}")]
    [RequirePermission(Permission.ViewPatients)]
    [ProducesResponseType(typeof(LetterResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLetter(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new GetLetterQuery(id, User.ToPathwayActor()), ct));

    /// <summary>Saves a draft: its text, large print and recipients (its author only).</summary>
    [HttpPut("{id:guid}")]
    [RequirePermission(Permission.WriteLetters)]
    [RequestSizeLimit(MaxRequestBytes)]
    [ProducesResponseType(typeof(LetterResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateLetter(Guid id, [FromBody] UpdateLetterRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new UpdateLetterCommand(
            id,
            request.Letterhead,
            request.Body,
            request.Closing,
            request.LargePrint,
            request.Recipients
                .Select(r => new LetterRecipientCommandItem(r.Kind, r.PracticeId, r.Name, r.AddressLines, r.Postcode, r.Email))
                .ToList(),
            request.ExpectedVersion,
            User.ToPathwayActor()), ct));

    /// <summary>
    /// A draft as a PDF, filled from the record now and marked "Draft, not approved" on every page: for its author, its
    /// approver, or anyone who approves letters. Sent as a download, like every file. Recorded as viewing it.
    /// </summary>
    [HttpGet("{id:guid}/preview")]
    [RequirePermission(Permission.ViewPatients)]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK, LetterPdf.ContentType)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> PreviewLetter(Guid id, CancellationToken ct)
    {
        var pdf = await mediator.Send(new PreviewLetterQuery(id, User.ToPathwayActor()), ct);
        var download = new AttachmentDownload(pdf.FileName, LetterPdf.ContentType, pdf.Content.Length, new MemoryStream(pdf.Content));
        AttachmentResponseHeaders.Apply(Response, download);
        return new FileStreamResult(download.Content, download.ContentType);
    }

    /// <summary>Submits a draft to a named holder of ApproveLetters, who is told.</summary>
    [HttpPost("{id:guid}/submit")]
    [RequirePermission(Permission.WriteLetters)]
    [ProducesResponseType(typeof(LetterResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SubmitLetter(Guid id, [FromBody] SubmitLetterRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new SubmitLetterCommand(id, request.ApproverUserId, request.ExpectedVersion, User.ToPathwayActor()), ct));

    /// <summary>Approves a letter: its PDF is made, kept as a file, and the letter frozen.</summary>
    [HttpPost("{id:guid}/approve")]
    [RequirePermission(Permission.ApproveLetters)]
    [ProducesResponseType(typeof(LetterResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ApproveLetter(Guid id, [FromBody] LetterVersionRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new ApproveLetterCommand(id, request.ExpectedVersion, User.ToPathwayActor()), ct));

    /// <summary>Returns a letter to its author, with a comment.</summary>
    [HttpPost("{id:guid}/return")]
    [RequirePermission(Permission.ApproveLetters)]
    [ProducesResponseType(typeof(LetterResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReturnLetter(Guid id, [FromBody] ReturnLetterRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new ReturnLetterCommand(id, request.Comment, request.ExpectedVersion, User.ToPathwayActor()), ct));

    /// <summary>Discards its author's draft.</summary>
    [HttpPost("{id:guid}/discard")]
    [RequirePermission(Permission.WriteLetters)]
    [ProducesResponseType(typeof(LetterResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DiscardLetter(Guid id, [FromBody] LetterVersionRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new DiscardLetterCommand(id, request.ExpectedVersion, User.ToPathwayActor()), ct));

    /// <summary>
    /// Withdraws an approved letter found wrong, with a reason: whoever approved it, or someone who can correct records
    /// (checked here, so the endpoint needs only ViewPatients).
    /// </summary>
    [HttpPost("{id:guid}/withdraw")]
    [RequirePermission(Permission.ViewPatients)]
    [ProducesResponseType(typeof(LetterResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> WithdrawLetter(Guid id, [FromBody] WithdrawLetterRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new WithdrawLetterCommand(id, request.Reason, request.ExpectedVersion, User.ToPathwayActor()), ct));

    /// <summary>Writes a correction of a withdrawn letter: a new draft with its text and recipients.</summary>
    [HttpPost("{id:guid}/correction")]
    [RequirePermission(Permission.WriteLetters)]
    [ProducesResponseType(typeof(LetterResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> WriteCorrection(Guid id, CancellationToken ct)
    {
        var letter = await mediator.Send(new WriteLetterCorrectionCommand(id, User.ToPathwayActor()), ct);
        return Created($"/api/letters/{letter.Id}", letter);
    }

    /// <summary>
    /// Sends an approved letter to one recipient by secure email from MedApp, synchronously: marked sent only when the
    /// mail server accepts it; a 409 saying so when it doesn't.
    /// </summary>
    [HttpPost("{id:guid}/recipients/{recipientId:guid}/send")]
    [RequirePermission(Permission.SendLetters)]
    [ProducesResponseType(typeof(LetterResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SendLetter(
        Guid id, Guid recipientId, [FromBody] LetterVersionRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new SendLetterCommand(id, recipientId, request.ExpectedVersion, User.ToPathwayActor()), ct));

    /// <summary>Records an approved letter as sent to one recipient another way: posted, handed to the patient, or emailed outside MedApp.</summary>
    [HttpPost("{id:guid}/recipients/{recipientId:guid}/record-sent")]
    [RequirePermission(Permission.SendLetters)]
    [ProducesResponseType(typeof(LetterResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RecordSent(
        Guid id, Guid recipientId, [FromBody] RecordLetterSentRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new RecordLetterSentCommand(
            id, recipientId, request.Method, request.SentOn, request.ExpectedVersion, User.ToPathwayActor()), ct));
}
