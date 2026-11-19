using MedApp.Application.Common.Identity;
using MedApp.API.Common.Authentication;
using MedApp.Application.Pathways.NoteDeletionRequests.Commands.RequestNoteDeletion;
using MedApp.Application.Pathways.PatientPathways.Commands.AddPathwayNote;
using MedApp.Application.Pathways.PatientPathways.Commands.AssignPatientPathway;
using MedApp.Application.Pathways.PatientPathways.Commands.ChangePatientPathwayStatus;
using MedApp.Application.Pathways.PatientPathways.Commands.CreatePatientPathway;
using MedApp.Application.Pathways.PatientPathways.Commands.DeletePatientPathway;
using MedApp.Application.Pathways.PatientPathways.Commands.DeletePathwayNote;
using MedApp.Application.Pathways.PatientPathways.Commands.EditPathwayNote;
using MedApp.Application.Pathways.PatientPathways.Commands.FinishPathwayStage;
using MedApp.Application.Pathways.PatientPathways.Commands.ReinstatePathwayStage;
using MedApp.Application.Pathways.PatientPathways.Commands.SkipPathwayStage;
using MedApp.Application.Pathways.PatientPathways.Commands.StartPathwayStage;
using MedApp.Application.Pathways.PatientPathways.Commands.UnassignPatientPathway;
using MedApp.Application.Pathways.PatientPathways.Commands.UnlockPathwayStage;
using MedApp.Application.Pathways.PatientPathways.Commands.UpdatePatientPathway;
using MedApp.Application.Pathways.PatientPathways.Commands.UpdatePathwayStageNotes;
using MedApp.Application.Pathways.PatientPathways.Queries.GetPatientPathwayById;
using MedApp.Application.Pathways.PatientPathways.Queries.GetPatientPathwayLabels;
using MedApp.Application.Pathways.PatientPathways.Queries.SearchPatientPathways;
using MedApp.Contracts.Common;
using MedApp.Contracts.Pathways.NoteDeletionRequests.Requests;
using MedApp.Contracts.Pathways.PatientPathways.Requests;
using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace MedApp.API.Controllers;

/// <summary>
/// Patients' pathways. Any staff member can create and work on any pathway; admins assign staff, unlock finished
/// stages and delete cancelled or archived pathways. Every change is audited.
/// </summary>
[ApiController]
[Route("api/pathways/patient-pathways")]
[Tags("Patient Pathways")]
public sealed class PatientPathwaysController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(PatientPathwayResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [RequirePermission(Permission.CreatePathways)]
    public async Task<IActionResult> CreatePatientPathway([FromBody] CreatePatientPathwayRequest request, CancellationToken ct)
    {
        var pathway = await mediator.Send(new CreatePatientPathwayCommand(
            request.PatientId,
            request.TemplateId,
            request.Title,
            request.Notes,
            request.DueDate,
            request.Priority,
            User.ToPathwayActor()), ct);

        return Created($"/api/pathways/patient-pathways/{pathway.Id}", pathway);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PatientPathwayResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [RequirePermission(Permission.ViewPatients)]
    public async Task<IActionResult> GetPatientPathwayById(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new GetPatientPathwayByIdQuery(id, User.GetUserId(), User.GetPermissions(), User.ToTeamScope()), ct));

    /// <summary>
    /// Pathways' titles and patients' names only, for the side nav's pins (at most 20). Not recorded in the activity
    /// log: a pin's label isn't a view of the record. Pathways that no longer exist are left out.
    /// </summary>
    [HttpGet("labels")]
    [ProducesResponseType(typeof(IReadOnlyList<PatientPathwayLabelResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [RequirePermission(Permission.ViewPatients)]
    public async Task<IActionResult> GetPatientPathwayLabels([FromQuery] List<Guid>? ids, CancellationToken ct) =>
        Ok(await mediator.Send(new GetPatientPathwayLabelsQuery(ids ?? [], User.ToTeamScope()), ct));

    /// <summary>One page of pathways. By default sorted by target date, soonest first, without closed ones.</summary>
    [HttpGet("search")]
    [ProducesResponseType(typeof(PagedResponse<PatientPathwaySummaryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [RequirePermission(Permission.ViewPatients)]
    public async Task<IActionResult> SearchPatientPathways([FromQuery] SearchPatientPathwaysRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new SearchPatientPathwaysQuery(
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
            request.NobodyAssigned,
            request.TeamIds,
            request.StageDefinitionIds,
            request.BreachedOnly,
            User.ToTeamScope()), ct));

    [HttpPatch("{id:guid}")]
    [ProducesResponseType(typeof(PatientPathwayResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [RequirePermission(Permission.WorkPathways)]
    public async Task<IActionResult> UpdatePatientPathway(Guid id, [FromBody] UpdatePatientPathwayRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new UpdatePatientPathwayCommand(
            id,
            request.Title,
            request.DueDate,
            request.Priority,
            User.ToPathwayActor(),
            request.Notes), ct));

    /// <summary>
    /// Adds a note to a pathway, by the signed-in user: whenever its details can be edited, so also once it's
    /// Completed, but not when Cancelled or Archived (409). Audited (NoteAdded, with the note's ID and version only).
    /// The 201's Location is the pathway, which the note is read with.
    /// </summary>
    [HttpPost("{id:guid}/notes")]
    [ProducesResponseType(typeof(PatientPathwayNoteResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [RequirePermission(Permission.WorkPathways)]
    public async Task<IActionResult> AddPathwayNote(Guid id, [FromBody] AddPathwayNoteRequest request, CancellationToken ct)
    {
        var note = await mediator.Send(new AddPathwayNoteCommand(id, request.Text, User.ToPathwayActor()), ct);
        // A note has no address of its own: it's read with its pathway.
        return Created($"/api/pathways/patient-pathways/{id}", note);
    }

    /// <summary>
    /// Edits one of your own notes on a pathway (anyone else's is a 403, admins' too), from the version you saw: if it
    /// has changed since, a 409. Audited (NoteEdited, with the note's ID and new version only).
    /// </summary>
    [HttpPut("{id:guid}/notes/{noteId:guid}")]
    [ProducesResponseType(typeof(PatientPathwayNoteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [RequirePermission(Permission.WorkPathways)]
    public async Task<IActionResult> EditPathwayNote(Guid id, Guid noteId, [FromBody] EditPathwayNoteRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new EditPathwayNoteCommand(id, noteId, request.Text, request.ExpectedVersion, User.ToPathwayActor()), ct));

    /// <summary>
    /// Deletes a note on a pathway (admins), with a reason: soft, so it's hidden from the pathway page but kept, with
    /// its versions, and in the patient's export. In any status of the pathway, but not while the patient's record is
    /// restricted or anonymised (409). A request to delete it that was waiting is approved, and its requester told.
    /// Audited (NoteDeleted, with the note's ID and version and the reason).
    /// </summary>
    [HttpPost("{id:guid}/notes/{noteId:guid}/delete")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [RequirePermission(Permission.CorrectRecords)]
    public async Task<IActionResult> DeletePathwayNote(Guid id, Guid noteId, [FromBody] DeletePathwayNoteRequest request, CancellationToken ct)
    {
        await mediator.Send(new DeletePathwayNoteCommand(
            id, noteId, request.Reason, request.Detail, request.ExpectedVersion, User.ToPathwayActor()), ct);
        return NoContent();
    }

    /// <summary>
    /// Asks an admin to delete a note on a pathway, with a reason: every admin but you is told, and one of them
    /// decides. Any note, in any status of the pathway, but not while the patient's record is restricted or anonymised,
    /// and one waiting request per note (409, naming who asked and when). Returns the note, showing the request. The 201's
    /// Location is the request (which only admins can read).
    /// </summary>
    [HttpPost("{id:guid}/notes/{noteId:guid}/deletion-requests")]
    [ProducesResponseType(typeof(PatientPathwayNoteResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [RequirePermission(Permission.WorkPathways)]
    public async Task<IActionResult> RequestNoteDeletion(Guid id, Guid noteId, [FromBody] RequestNoteDeletionRequest request, CancellationToken ct)
    {
        var note = await mediator.Send(new RequestNoteDeletionCommand(id, noteId, request.Reason, request.Detail, User.ToPathwayActor()), ct);
        return Created($"/api/pathways/note-deletion-requests/{note.DeletionRequestId}", note);
    }

    /// <summary>Blocks, unblocks, cancels, completes or archives a pathway (see its allowedStatusChanges).</summary>
    [HttpPost("{id:guid}/status")]
    [ProducesResponseType(typeof(PatientPathwayResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [RequirePermission(Permission.WorkPathways)]
    public async Task<IActionResult> ChangePatientPathwayStatus(Guid id, [FromBody] ChangePatientPathwayStatusRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new ChangePatientPathwayStatusCommand(
            id,
            request.Status,
            request.Reason,
            User.ToPathwayActor()), ct));

    /// <summary>Deletes a cancelled or archived pathway. Its audit history is kept.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [RequirePermission(Permission.DeletePathways)]
    public async Task<IActionResult> DeletePatientPathway(Guid id, CancellationToken ct)
    {
        await mediator.Send(new DeletePatientPathwayCommand(id, User.ToPathwayActor()), ct);
        return NoContent();
    }

    /// <summary>
    /// Assigns someone to the pathway. Only holders of <c>WorkPathways</c> can be assigned: anyone else is a 400 on
    /// <c>UserId</c>.
    /// </summary>
    [HttpPost("{id:guid}/assignments/{userId:guid}")]
    [ProducesResponseType(typeof(PatientPathwayResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [RequirePermission(Permission.AssignPathways)]
    public async Task<IActionResult> AssignPatientPathway(Guid id, Guid userId, CancellationToken ct) =>
        Ok(await mediator.Send(new AssignPatientPathwayCommand(id, userId, User.ToPathwayActor()), ct));

    [HttpDelete("{id:guid}/assignments/{userId:guid}")]
    [ProducesResponseType(typeof(PatientPathwayResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [RequirePermission(Permission.AssignPathways)]
    public async Task<IActionResult> UnassignPatientPathway(Guid id, Guid userId, CancellationToken ct) =>
        Ok(await mediator.Send(new UnassignPatientPathwayCommand(id, userId, User.ToPathwayActor()), ct));

    [HttpPost("{id:guid}/stages/{stageId:guid}/start")]
    [ProducesResponseType(typeof(PatientPathwayResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [RequirePermission(Permission.WorkPathways)]
    public async Task<IActionResult> StartPathwayStage(Guid id, Guid stageId, CancellationToken ct) =>
        Ok(await mediator.Send(new StartPathwayStageCommand(id, stageId, User.ToPathwayActor()), ct));

    [HttpPut("{id:guid}/stages/{stageId:guid}/notes")]
    [ProducesResponseType(typeof(PatientPathwayResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [RequirePermission(Permission.WorkPathways)]
    public async Task<IActionResult> UpdatePathwayStageNotes(Guid id, Guid stageId, [FromBody] UpdatePathwayStageNotesRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new UpdatePathwayStageNotesCommand(id, stageId, request.Notes, User.ToPathwayActor()), ct));

    /// <summary>
    /// Finishes a stage. A stage whose visit is required needs an attended appointment first (409), unless an admin
    /// sends <c>{ withoutVisit: true, reason }</c>. No body works as before.
    /// </summary>
    [HttpPost("{id:guid}/stages/{stageId:guid}/finish")]
    [ProducesResponseType(typeof(PatientPathwayResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [RequirePermission(Permission.WorkPathways)]
    public async Task<IActionResult> FinishPathwayStage(
        Guid id,
        Guid stageId,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] FinishPathwayStageRequest? request,
        CancellationToken ct) =>
        Ok(await mediator.Send(new FinishPathwayStageCommand(
            id,
            stageId,
            User.ToPathwayActor(),
            request?.WithoutVisit ?? false,
            request?.Reason,
            User.HasPermission(Permission.FinishWithoutVisit),
            request?.Branch), ct));

    /// <summary>Reopens a finished stage for editing.</summary>
    [HttpPost("{id:guid}/stages/{stageId:guid}/unlock")]
    [ProducesResponseType(typeof(PatientPathwayResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [RequirePermission(Permission.UnlockStages)]
    public async Task<IActionResult> UnlockPathwayStage(Guid id, Guid stageId, CancellationToken ct) =>
        Ok(await mediator.Send(new UnlockPathwayStageCommand(id, stageId, User.ToPathwayActor()), ct));

    /// <summary>
    /// Skips an optional stage while it's current, with a reason (NotNeeded, PatientDeclined or Other, with a detail for
    /// Other). The pathway moves on to its next stage. One whose visit is Required needs FinishWithoutVisit and a detail (409, 400).
    /// </summary>
    [HttpPost("{id:guid}/stages/{stageId:guid}/skip")]
    [ProducesResponseType(typeof(PatientPathwayResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [RequirePermission(Permission.WorkPathways)]
    public async Task<IActionResult> SkipPathwayStage(Guid id, Guid stageId, [FromBody] SkipPathwayStageRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new SkipPathwayStageCommand(
            id, stageId, request.Reason, request.Detail, User.ToPathwayActor(), User.HasPermission(Permission.FinishWithoutVisit)), ct));

    /// <summary>
    /// Reinstates a skipped stage, with a reason, so it runs again in its place (before the current stage, it becomes
    /// current). A branch can be changed only by reinstating its stages.
    /// </summary>
    [HttpPost("{id:guid}/stages/{stageId:guid}/reinstate")]
    [ProducesResponseType(typeof(PatientPathwayResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [RequirePermission(Permission.UnlockStages)]
    public async Task<IActionResult> ReinstatePathwayStage(Guid id, Guid stageId, [FromBody] ReinstatePathwayStageRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new ReinstatePathwayStageCommand(id, stageId, request.Reason, User.ToPathwayActor()), ct));
}
