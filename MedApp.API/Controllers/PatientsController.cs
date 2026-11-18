using Microsoft.AspNetCore.Mvc;
using MedApp.Application.Patients.Commands.AnonymisePatient;
using MedApp.Application.Patients.Commands.CreatePatient;
using MedApp.Application.Patients.Commands.KeepPatientRecord;
using MedApp.Application.Patients.Commands.SetPatientFlag;
using MedApp.Application.Patients.Queries.ExportPatient;
using MedApp.Application.Patients.Queries.SearchRetentionReview;
using MedApp.Application.Patients.Commands.DeletePatient;
using MedApp.Application.Patients.Commands.UpdatePatient;
using MedApp.Application.Patients.Queries.GetAllPatients;
using MedApp.Application.Patients.Queries.GetPatientById;
using MedApp.Application.Patients.Queries.GetPatientLabels;
using MedApp.Application.Patients.Queries.SearchPatients;
using MedApp.Contracts.Common;
using MedApp.Contracts.Patients.Requests;
using MedApp.Contracts.Patients.Responses;
using MedApp.API.Common.Authentication;
using MediatR;
using Microsoft.AspNetCore.Authorization;

namespace MedApp.API.Controllers;

[ApiController]
[Route("api/patients")]
public sealed class PatientsController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(PatientResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [Authorize(Policy = AuthPolicies.Staff)]
    public async Task<IActionResult> CreatePatient([FromBody] CreatePatientRequest request)
    {
        var command = new CreatePatientCommand(
            request.FirstName,
            request.LastName,
            request.DateOfBirth,
            request.Email,
            request.NhsNumber
        );

        var patient = await mediator.Send(command);

        return CreatedAtAction(
            nameof(GetPatientById),
            new { id = patient.Id },
            patient
        );
    }


    
    /// <summary>
    /// Patients' names only, for the side nav's pins (at most 20). Not recorded in the activity log: a pin's label isn't
    /// a view of the record. Patients that no longer exist are left out.
    /// </summary>
    [HttpGet("labels")]
    [ProducesResponseType(typeof(IReadOnlyList<PatientLabelResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [Authorize(Policy = AuthPolicies.Staff)]
    public async Task<IActionResult> GetPatientLabels([FromQuery] List<Guid>? ids, CancellationToken ct) =>
        Ok(await mediator.Send(new GetPatientLabelsQuery(ids ?? []), ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PatientResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Authorize(Policy = AuthPolicies.Staff)]
    public async Task<IActionResult> GetPatientById(Guid id)
    {
        var query = new GetPatientByIdQuery(id);

        var patient = await mediator.Send(query);

        if (patient is null)
            return NotFound();

        return Ok(patient);
    }

    /// <summary>
    /// One page of patients, filtered and sorted on the server. Used by MedApp.Web; the Blazor client still loads
    /// the whole list from <c>GET api/patients</c>, which goes once it is retired.
    /// </summary>
    [HttpGet("search")]
    [ProducesResponseType(typeof(PagedResponse<PatientResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [Authorize(Policy = AuthPolicies.Staff)]
    public async Task<IActionResult> SearchPatients([FromQuery] SearchPatientsRequest request, CancellationToken cancellationToken)
    {
        var query = new SearchPatientsQuery(
            request.Page,
            request.PageSize,
            request.Sort,
            request.Ascending,
            request.FirstName,
            request.LastName,
            request.Email,
            request.DateOfBirthFrom,
            request.DateOfBirthTo,
            request.NhsNumber);

        return Ok(await mediator.Send(query, cancellationToken));
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<PatientResponse>), StatusCodes.Status200OK)]
    [Authorize(Policy = AuthPolicies.Staff)]
    public async Task<IActionResult> GetAllPatients()
    {
        var query = new GetAllPatientsQuery();

        var patients = await mediator.Send(query);

        return Ok(patients);
    }
    
    [HttpPatch("{id:guid}")]
    [ProducesResponseType(typeof(PatientResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [Authorize(Policy = AuthPolicies.Staff)]
    public async Task<IActionResult> UpdatePatient(
        Guid id,
        [FromBody] UpdatePatientRequest request)
    {
        var command = new UpdatePatientCommand(
            id,
            request.FirstName,
            request.LastName,
            request.DateOfBirth,
            request.Email,
            request.NhsNumber
        );

        var patient = await mediator.Send(command);

        if (patient is null)
            return NotFound();

        return Ok(patient);
    }
    
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [Authorize(Policy = AuthPolicies.Staff)]
    public async Task<IActionResult> DeletePatient(Guid id)
    {
        var command = new DeletePatientCommand(id);

        var deleted = await mediator.Send(command);

        if (!deleted)
            return NotFound();

        return NoContent();
    }

    /// <summary>
    /// Everything held about a patient, for a subject access request: details, pathways with stages, notes and
    /// assignments, the history of changes, the access log and notifications. Recorded in the activity log, and never
    /// cached.
    /// </summary>
    [HttpGet("{id:guid}/export")]
    [ProducesResponseType(typeof(PatientExportResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ExportPatient(Guid id, CancellationToken ct)
    {
        var export = await mediator.Send(new ExportPatientQuery(id), ct);
        Response.Headers.CacheControl = "no-store";
        return Ok(export);
    }

    /// <summary>
    /// Restricts the record (Article 18), so nothing about the patient or their pathways can be changed, or lifts
    /// the restriction. Setting it needs a reason, shown on the patient's banner.
    /// </summary>
    [HttpPut("{id:guid}/restriction")]
    [ProducesResponseType(typeof(PatientResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SetRestriction(Guid id, [FromBody] SetPatientFlagRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new SetPatientFlagCommand(id, PatientFlag.Restricted, request.Set, request.Reason, request.ConfirmOpenPathways), ct));

    /// <summary>Marks the record as disputed by the patient (Article 16), with what they dispute, or clears it.</summary>
    [HttpPut("{id:guid}/dispute")]
    [ProducesResponseType(typeof(PatientResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SetDispute(Guid id, [FromBody] SetPatientFlagRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new SetPatientFlagCommand(id, PatientFlag.Disputed, request.Set, request.Reason), ct));

    /// <summary>
    /// Anonymises the record: identifying details and free text become placeholders, and the rest stays. Confirmed by
    /// the patient's full name. Refused while a pathway is under way, an appointment is open (Requested or Booked), or
    /// the record is restricted (409).
    /// </summary>
    [HttpPost("{id:guid}/anonymise")]
    [ProducesResponseType(typeof(PatientResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AnonymisePatient(Guid id, [FromBody] AnonymisePatientRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new AnonymisePatientCommand(id, request.Reason ?? string.Empty, request.Note, request.Confirmation ?? string.Empty), ct));

    /// <summary>
    /// The patients whose records are past their retention period and due for review, the longest overdue first.
    /// Nothing is ever deleted automatically.
    /// </summary>
    [HttpGet("retention-review")]
    [ProducesResponseType(typeof(PagedResponse<RetentionReviewItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SearchRetentionReview([FromQuery] SearchRetentionReviewRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new SearchRetentionReviewQuery(request.Page, request.PageSize, request.IncludeKept), ct));

    /// <summary>Keeps a record past its retention period, with a reason, until a review date up to two years ahead.</summary>
    [HttpPost("{id:guid}/retention/keep")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> KeepPatientRecord(Guid id, [FromBody] KeepPatientRecordRequest request, CancellationToken ct)
    {
        await mediator.Send(new KeepPatientRecordCommand(id, request.KeepUntil, request.Reason ?? string.Empty), ct);
        return NoContent();
    }
}