using MedApp.API.Common.Authentication;
using MedApp.Application.Procedures.PatientProcedures.Commands.AssignPatientProcedure;
using MedApp.Application.Procedures.PatientProcedures.Commands.ChangePatientProcedureStatus;
using MedApp.Application.Procedures.PatientProcedures.Commands.CreatePatientProcedure;
using MedApp.Application.Procedures.PatientProcedures.Commands.DeletePatientProcedure;
using MedApp.Application.Procedures.PatientProcedures.Commands.FinishProcedureStage;
using MedApp.Application.Procedures.PatientProcedures.Commands.StartProcedureStage;
using MedApp.Application.Procedures.PatientProcedures.Commands.UnassignPatientProcedure;
using MedApp.Application.Procedures.PatientProcedures.Commands.UnlockProcedureStage;
using MedApp.Application.Procedures.PatientProcedures.Commands.UpdatePatientProcedure;
using MedApp.Application.Procedures.PatientProcedures.Commands.UpdateProcedureStageNotes;
using MedApp.Application.Procedures.PatientProcedures.Queries.GetPatientProcedureById;
using MedApp.Application.Procedures.PatientProcedures.Queries.GetPatientProcedureLabels;
using MedApp.Application.Procedures.PatientProcedures.Queries.SearchPatientProcedures;
using MedApp.Contracts.Common;
using MedApp.Contracts.Procedures.PatientProcedures.Requests;
using MedApp.Contracts.Procedures.PatientProcedures.Responses;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace MedApp.API.Controllers;

/// <summary>
/// Patients' procedures. Any staff member can create and work on any procedure; admins assign staff, unlock finished
/// stages and delete cancelled or archived procedures. Every change is audited.
/// </summary>
[ApiController]
[Route("api/procedures/patient-procedures")]
[Tags("Patient Procedures")]
public sealed class PatientProceduresController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(PatientProcedureResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [Authorize(Policy = AuthPolicies.Staff)]
    public async Task<IActionResult> CreatePatientProcedure([FromBody] CreatePatientProcedureRequest request, CancellationToken ct)
    {
        var procedure = await mediator.Send(new CreatePatientProcedureCommand(
            request.PatientId,
            request.TemplateId,
            request.Title,
            request.Notes,
            request.DueDate,
            request.Priority,
            User.ToProcedureActor()), ct);

        return Created($"/api/procedures/patient-procedures/{procedure.Id}", procedure);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PatientProcedureResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [Authorize(Policy = AuthPolicies.Staff)]
    public async Task<IActionResult> GetPatientProcedureById(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new GetPatientProcedureByIdQuery(id), ct));

    /// <summary>
    /// Procedures' titles and patients' names only, for the side nav's pins (at most 20). Not recorded in the activity
    /// log: a pin's label isn't a view of the record. Procedures that no longer exist are left out.
    /// </summary>
    [HttpGet("labels")]
    [ProducesResponseType(typeof(IReadOnlyList<PatientProcedureLabelResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [Authorize(Policy = AuthPolicies.Staff)]
    public async Task<IActionResult> GetPatientProcedureLabels([FromQuery] List<Guid>? ids, CancellationToken ct) =>
        Ok(await mediator.Send(new GetPatientProcedureLabelsQuery(ids ?? []), ct));

    /// <summary>One page of procedures. By default sorted by due date, soonest first, without closed ones.</summary>
    [HttpGet("search")]
    [ProducesResponseType(typeof(PagedResponse<PatientProcedureSummaryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [Authorize(Policy = AuthPolicies.Staff)]
    public async Task<IActionResult> SearchPatientProcedures([FromQuery] SearchPatientProceduresRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new SearchPatientProceduresQuery(
            request.Page,
            request.PageSize,
            request.Sort,
            request.Ascending,
            request.Search,
            request.PatientId,
            request.TemplateId,
            request.Statuses,
            request.Priority,
            request.AssignedToUserId,
            request.AssignedToMe,
            request.OverdueOnly,
            request.DueFrom,
            request.DueTo,
            request.IncludeClosed,
            request.Started,
            User.GetUserId(),
            request.Priorities,
            request.Title,
            request.TemplateName,
            request.PatientName,
            request.NobodyAssigned), ct));

    [HttpPatch("{id:guid}")]
    [ProducesResponseType(typeof(PatientProcedureResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [Authorize(Policy = AuthPolicies.Staff)]
    public async Task<IActionResult> UpdatePatientProcedure(Guid id, [FromBody] UpdatePatientProcedureRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new UpdatePatientProcedureCommand(
            id,
            request.Title,
            request.Notes,
            request.DueDate,
            request.Priority,
            User.ToProcedureActor()), ct));

    /// <summary>Blocks, unblocks, cancels, completes or archives a procedure (see its allowedStatusChanges).</summary>
    [HttpPost("{id:guid}/status")]
    [ProducesResponseType(typeof(PatientProcedureResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [Authorize(Policy = AuthPolicies.Staff)]
    public async Task<IActionResult> ChangePatientProcedureStatus(Guid id, [FromBody] ChangePatientProcedureStatusRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new ChangePatientProcedureStatusCommand(
            id,
            request.Status,
            request.Reason,
            User.ToProcedureActor()), ct));

    /// <summary>Deletes a cancelled or archived procedure. Its audit history is kept.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeletePatientProcedure(Guid id, CancellationToken ct)
    {
        await mediator.Send(new DeletePatientProcedureCommand(id, User.ToProcedureActor()), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/assignments/{userId:guid}")]
    [ProducesResponseType(typeof(PatientProcedureResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AssignPatientProcedure(Guid id, Guid userId, CancellationToken ct) =>
        Ok(await mediator.Send(new AssignPatientProcedureCommand(id, userId, User.ToProcedureActor()), ct));

    [HttpDelete("{id:guid}/assignments/{userId:guid}")]
    [ProducesResponseType(typeof(PatientProcedureResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UnassignPatientProcedure(Guid id, Guid userId, CancellationToken ct) =>
        Ok(await mediator.Send(new UnassignPatientProcedureCommand(id, userId, User.ToProcedureActor()), ct));

    [HttpPost("{id:guid}/stages/{stageId:guid}/start")]
    [ProducesResponseType(typeof(PatientProcedureResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [Authorize(Policy = AuthPolicies.Staff)]
    public async Task<IActionResult> StartProcedureStage(Guid id, Guid stageId, CancellationToken ct) =>
        Ok(await mediator.Send(new StartProcedureStageCommand(id, stageId, User.ToProcedureActor()), ct));

    [HttpPut("{id:guid}/stages/{stageId:guid}/notes")]
    [ProducesResponseType(typeof(PatientProcedureResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [Authorize(Policy = AuthPolicies.Staff)]
    public async Task<IActionResult> UpdateProcedureStageNotes(Guid id, Guid stageId, [FromBody] UpdateProcedureStageNotesRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new UpdateProcedureStageNotesCommand(id, stageId, request.Notes, User.ToProcedureActor()), ct));

    /// <summary>
    /// Finishes a stage. A stage whose visit is required needs an attended appointment first (409), unless an admin
    /// sends <c>{ withoutVisit: true, reason }</c>. No body works as before.
    /// </summary>
    [HttpPost("{id:guid}/stages/{stageId:guid}/finish")]
    [ProducesResponseType(typeof(PatientProcedureResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [Authorize(Policy = AuthPolicies.Staff)]
    public async Task<IActionResult> FinishProcedureStage(
        Guid id,
        Guid stageId,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] FinishProcedureStageRequest? request,
        CancellationToken ct) =>
        Ok(await mediator.Send(new FinishProcedureStageCommand(
            id,
            stageId,
            User.ToProcedureActor(),
            request?.WithoutVisit ?? false,
            request?.Reason,
            User.IsInRole("Admin")), ct));

    /// <summary>Reopens a finished stage for editing.</summary>
    [HttpPost("{id:guid}/stages/{stageId:guid}/unlock")]
    [ProducesResponseType(typeof(PatientProcedureResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UnlockProcedureStage(Guid id, Guid stageId, CancellationToken ct) =>
        Ok(await mediator.Send(new UnlockProcedureStageCommand(id, stageId, User.ToProcedureActor()), ct));
}
