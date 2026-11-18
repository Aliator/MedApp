using MedApp.API.Common.Authentication;
using MedApp.Application.Common.Identity;
using MedApp.Application.Practices.Commands.CreatePractice;
using MedApp.Application.Practices.Commands.DeletePractice;
using MedApp.Application.Practices.Commands.SetPracticeArchived;
using MedApp.Application.Practices.Commands.UpdatePractice;
using MedApp.Application.Practices.Queries.GetPracticeById;
using MedApp.Application.Practices.Queries.GetPractices;
using MedApp.Contracts.Practices.Requests;
using MedApp.Contracts.Practices.Responses;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace MedApp.API.Controllers;

/// <summary>
/// The GP practice directory: the practices patients are registered with and referrals come from. Holders of
/// ViewPatients read it; holders of EditPatients keep it. One that a patient or referral has can't be deleted, only
/// archived (it can't be chosen again) and restored. Every change carries the version the caller saw
/// (<c>expectedVersion</c>): 409 if someone has changed it since. It holds organisations only, so reading it records
/// nothing in the activity log.
/// </summary>
[ApiController]
[Route("api/practices")]
[Tags("Practices")]
public sealed class PracticesController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [RequirePermission(Permission.ViewPatients)]
    [ProducesResponseType(typeof(IReadOnlyList<PracticeResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPractices([FromQuery] bool includeArchived, CancellationToken ct) =>
        Ok(await mediator.Send(new GetPracticesQuery(includeArchived), ct));

    [HttpGet("{id:guid}")]
    [RequirePermission(Permission.ViewPatients)]
    [ProducesResponseType(typeof(PracticeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPractice(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new GetPracticeByIdQuery(id), ct));

    [HttpPost]
    [RequirePermission(Permission.EditPatients)]
    [ProducesResponseType(typeof(PracticeResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreatePractice([FromBody] CreatePracticeRequest request, CancellationToken ct)
    {
        var practice = await mediator.Send(new CreatePracticeCommand(
            request.Name,
            request.OdsCode,
            request.AddressLines,
            request.Postcode,
            request.Phone,
            request.SecureEmail), ct);
        return Created($"/api/practices/{practice.Id}", practice);
    }

    /// <summary>Replaces a practice's details; patients and referrals that have it show the new ones.</summary>
    [HttpPut("{id:guid}")]
    [RequirePermission(Permission.EditPatients)]
    [ProducesResponseType(typeof(PracticeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdatePractice(Guid id, [FromBody] UpdatePracticeRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new UpdatePracticeCommand(
            id,
            request.Name,
            request.OdsCode,
            request.AddressLines,
            request.Postcode,
            request.Phone,
            request.SecureEmail,
            request.ExpectedVersion), ct));

    /// <summary>Archives a practice: it can't be chosen for a patient or referral; those that have it keep it.</summary>
    [HttpPost("{id:guid}/archive")]
    [RequirePermission(Permission.EditPatients)]
    [ProducesResponseType(typeof(PracticeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ArchivePractice(Guid id, [FromBody] PracticeVersionRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new SetPracticeArchivedCommand(id, true, request.ExpectedVersion), ct));

    [HttpPost("{id:guid}/restore")]
    [RequirePermission(Permission.EditPatients)]
    [ProducesResponseType(typeof(PracticeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RestorePractice(Guid id, [FromBody] PracticeVersionRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new SetPracticeArchivedCommand(id, false, request.ExpectedVersion), ct));

    /// <summary>Deletes a practice nothing has; one that a patient or referral has is a 409 (archive it).</summary>
    [HttpDelete("{id:guid}")]
    [RequirePermission(Permission.EditPatients)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeletePractice(Guid id, CancellationToken ct)
    {
        await mediator.Send(new DeletePracticeCommand(id), ct);
        return NoContent();
    }
}
