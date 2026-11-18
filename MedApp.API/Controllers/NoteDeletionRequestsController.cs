using MedApp.API.Common.Authentication;
using MedApp.Application.Pathways.NoteDeletionRequests.Commands.DecideNoteDeletionRequest;
using MedApp.Application.Pathways.NoteDeletionRequests.Queries.GetNoteDeletionRequest;
using MedApp.Contracts.Pathways.NoteDeletionRequests.Requests;
using MedApp.Contracts.Pathways.NoteDeletionRequests.Responses;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedApp.API.Controllers;

/// <summary>
/// Requests from staff to delete notes on pathways, for admins to decide: delete the note (soft, with a reason), or
/// keep it. Staff make them on the pathway's note (<c>POST api/pathways/patient-pathways/{id}/notes/{noteId}/deletion-requests</c>).
/// </summary>
[ApiController]
[Route("api/pathways/note-deletion-requests")]
[Tags("Note Deletion Requests")]
[Authorize(Roles = "Admin")]
public sealed class NoteDeletionRequestsController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// A request, with the note's text, the patient (as the patient banner shows them), the pathway's title, who asked,
    /// when and why, and its outcome once decided. Recorded in the activity log as a view of the pathway.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(NoteDeletionRequestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetNoteDeletionRequest(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new GetNoteDeletionRequestQuery(id), ct));

    /// <summary>
    /// Decides a request: delete the note (with the reason given, or the requester's) or keep it. The requester is told.
    /// A request already decided, or whose patient's record is restricted or anonymised, is a 409.
    /// </summary>
    [HttpPost("{id:guid}/decision")]
    [ProducesResponseType(typeof(NoteDeletionRequestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DecideNoteDeletionRequest(Guid id, [FromBody] DecideNoteDeletionRequestRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new DecideNoteDeletionRequestCommand(
            id, request.DeleteNote, request.ExpectedVersion, request.Reason, request.Detail, User.ToPathwayActor()), ct));
}
