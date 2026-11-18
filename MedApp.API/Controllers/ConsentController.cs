using MedApp.API.Common.Authentication;
using MedApp.Application.Common.Identity;
using MedApp.Application.Consent.Commands.AttachConsentScan;
using MedApp.Application.Consent.Commands.MarkConsentEnteredInError;
using MedApp.Application.Consent.Commands.RecordConsent;
using MedApp.Application.Consent.Commands.WithdrawConsent;
using MedApp.Application.Consent.Queries.GetPathwayConsentRecords;
using MedApp.Application.Consent.Setup;
using MedApp.Contracts.Consent;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace MedApp.API.Controllers;

/// <summary>
/// Consent records (<c>docs/centre-phases-3-5-plan.md</c>, decisions 145 to 147): a patient's consent to a stage of
/// their pathway, recorded by a holder of RecordConsent, with the form, the date signed, who signed and the scanned
/// form (a file on the pathway). A stage that needs consent can't be finished without a Valid record. <b>MedApp records
/// what the clinician states; it never assesses capacity, validity or whether the form fits.</b> Consent forms are
/// organisation-wide setup (ManageSetup; everyone reads them). Every change to a record carries the version the caller
/// saw (<c>expectedVersion</c>) and is audited.
/// </summary>
[ApiController]
[Route("api")]
public sealed class ConsentController(IMediator mediator) : ControllerBase
{
    private const string RecordsTag = "Consent records";
    private const string FormsTag = "Consent forms";

    /// <summary>A pathway's consent records, in any status, oldest first. Recorded as viewing the pathway.</summary>
    [HttpGet("pathways/{id:guid}/consent-records")]
    [RequirePermission(Permission.ViewPatients)]
    [ProducesResponseType(typeof(IReadOnlyList<ConsentRecordResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [Tags(RecordsTag)]
    public async Task<IActionResult> GetPathwayConsentRecords(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new GetPathwayConsentRecordsQuery(id, User.ToPathwayActor()), ct));

    /// <summary>Records consent to one of the pathway's stages (one still to do, while the pathway is open).</summary>
    [HttpPost("pathways/{id:guid}/consent-records")]
    [RequirePermission(Permission.RecordConsent)]
    [ProducesResponseType(typeof(ConsentRecordResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [Tags(RecordsTag)]
    public async Task<IActionResult> RecordConsent(Guid id, [FromBody] RecordConsentRequest request, CancellationToken ct)
    {
        var record = await mediator.Send(new RecordConsentCommand(
            id,
            request.StageId,
            request.ConsentFormId,
            request.SignedOn,
            request.SignedBy,
            request.ScanAttachmentId,
            User.ToPathwayActor()), ct);

        return Created($"/api/pathways/{id}/consent-records", record);
    }

    /// <summary>Records that consent was withdrawn (allowed while the record is restricted, and in any pathway status).</summary>
    [HttpPost("consent-records/{id:guid}/withdraw")]
    [RequirePermission(Permission.RecordConsent)]
    [ProducesResponseType(typeof(ConsentRecordResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [Tags(RecordsTag)]
    public async Task<IActionResult> WithdrawConsent(Guid id, [FromBody] WithdrawConsentRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new WithdrawConsentCommand(
            id, request.WithdrawnOn, request.Detail, request.ExpectedVersion, User.ToPathwayActor()), ct));

    /// <summary>
    /// Marks a record entered in error: whoever recorded it within 24 hours, or a holder of CorrectRecords (the handler
    /// checks). Its scan is deleted with it.
    /// </summary>
    [HttpPost("consent-records/{id:guid}/entered-in-error")]
    [RequirePermission(Permission.ViewPatients)]
    [ProducesResponseType(typeof(ConsentRecordResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [Tags(RecordsTag)]
    public async Task<IActionResult> MarkConsentEnteredInError(Guid id, [FromBody] MarkConsentEnteredInErrorRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new MarkConsentEnteredInErrorCommand(
            id, request.Detail, request.ExpectedVersion, User.ToPathwayActor()), ct));

    /// <summary>Gives a record without one its scanned form: a file already attached to the pathway or one of its stages.</summary>
    [HttpPost("consent-records/{id:guid}/scan")]
    [RequirePermission(Permission.RecordConsent)]
    [ProducesResponseType(typeof(ConsentRecordResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [Tags(RecordsTag)]
    public async Task<IActionResult> AttachConsentScan(Guid id, [FromBody] AttachConsentScanRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new AttachConsentScanCommand(
            id, request.AttachmentId, request.ExpectedVersion, User.ToPathwayActor()), ct));

    /// <summary>Every consent form by name, then version; archived ones only with <c>includeArchived</c>.</summary>
    [HttpGet("consent-forms")]
    [RequirePermission(Permission.UseApp)]
    [ProducesResponseType(typeof(IReadOnlyList<ConsentFormResponse>), StatusCodes.Status200OK)]
    [Tags(FormsTag)]
    public async Task<IActionResult> GetConsentForms([FromQuery] bool includeArchived, CancellationToken ct) =>
        Ok(await mediator.Send(new GetConsentFormsQuery(includeArchived), ct));

    [HttpPost("consent-forms")]
    [RequirePermission(Permission.ManageSetup)]
    [ProducesResponseType(typeof(ConsentFormResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [Tags(FormsTag)]
    public async Task<IActionResult> CreateConsentForm([FromBody] ConsentFormRequest request, CancellationToken ct)
    {
        var form = await mediator.Send(new CreateConsentFormCommand(request.Name, request.FormVersion), ct);
        return Created($"/api/consent-forms/{form.Id}", form);
    }

    [HttpPut("consent-forms/{id:guid}")]
    [RequirePermission(Permission.ManageSetup)]
    [ProducesResponseType(typeof(ConsentFormResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [Tags(FormsTag)]
    public async Task<IActionResult> UpdateConsentForm(Guid id, [FromBody] ConsentFormRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new UpdateConsentFormCommand(id, request.Name, request.FormVersion), ct));

    [HttpPost("consent-forms/{id:guid}/archive")]
    [RequirePermission(Permission.ManageSetup)]
    [ProducesResponseType(typeof(ConsentFormResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [Tags(FormsTag)]
    public async Task<IActionResult> ArchiveConsentForm(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new SetConsentFormArchivedCommand(id, true), ct));

    [HttpPost("consent-forms/{id:guid}/restore")]
    [RequirePermission(Permission.ManageSetup)]
    [ProducesResponseType(typeof(ConsentFormResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [Tags(FormsTag)]
    public async Task<IActionResult> RestoreConsentForm(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new SetConsentFormArchivedCommand(id, false), ct));

    /// <summary>Deletes a form no record has used; a used one is a 409 (archive it instead).</summary>
    [HttpDelete("consent-forms/{id:guid}")]
    [RequirePermission(Permission.ManageSetup)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [Tags(FormsTag)]
    public async Task<IActionResult> DeleteConsentForm(Guid id, CancellationToken ct)
    {
        await mediator.Send(new DeleteConsentFormCommand(id), ct);
        return NoContent();
    }
}
