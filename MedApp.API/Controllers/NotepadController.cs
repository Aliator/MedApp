using MedApp.API.Common.Authentication;
using MedApp.Application.Notepads.Commands.SaveNotepad;
using MedApp.Application.Notepads.Queries.GetNotepad;
using MedApp.Contracts.Notepads.Requests;
using MedApp.Contracts.Notepads.Responses;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedApp.API.Controllers;

/// <summary>
/// The signed-in user's own notepad: one each, kept on the server (never in the browser, as it may hold patient
/// details), cleared after <c>Notepad:KeepDays</c> without a change. Open to anyone signed in. Nothing here is
/// recorded in the activity log: it's the user's own, and not linked to a patient.
/// </summary>
[ApiController]
[Route("api/me/notepad")]
[Tags("Notepad")]
[Authorize]
public sealed class NotepadController(IMediator mediator) : ControllerBase
{
    /// <summary>Your notepad: empty, version 0, when you have none (or it was cleared after days without a change).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(NotepadResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetNotepad(CancellationToken ct) =>
        Ok(await mediator.Send(new GetNotepadQuery(User.GetUserId()), ct));

    /// <summary>
    /// Saves your whole notepad (empty clears it), from the version you last read or saved: a 409 if it was saved
    /// somewhere else since (another tab), or cleared after days without a change.
    /// </summary>
    [HttpPut]
    [ProducesResponseType(typeof(NotepadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SaveNotepad([FromBody] SaveNotepadRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new SaveNotepadCommand(User.GetUserId(), request.Text, request.ExpectedVersion), ct));
}
