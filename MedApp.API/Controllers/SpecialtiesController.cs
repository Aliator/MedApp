using MedApp.API.Common.Authentication;
using MedApp.Application.Common.Identity;
using MedApp.Application.Specialties.Commands.CreateSpecialty;
using MedApp.Application.Specialties.Commands.DeleteSpecialty;
using MedApp.Application.Specialties.Commands.SetSpecialtyArchived;
using MedApp.Application.Specialties.Commands.UpdateSpecialty;
using MedApp.Application.Specialties.Queries.GetSpecialties;
using MedApp.Application.Specialties.Queries.GetSpecialtyById;
using MedApp.Contracts.Specialties.Requests;
using MedApp.Contracts.Specialties.Responses;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace MedApp.API.Controllers;

/// <summary>
/// The specialties the centre offers (organisation-wide: holders of ManageSetup manage them, anyone with a role reads them). Every pathway template
/// belongs to one, a pathway keeps its template's, and staff belong to any number (<c>api/auth/users/{username}/specialties</c>).
/// One that's in use can't be deleted, only archived (it can't be chosen again) and restored. Every change carries the
/// version the caller saw (<c>expectedVersion</c>): 409 if another admin has changed it since.
/// </summary>
[ApiController]
[Route("api/specialties")]
[Tags("Specialties")]
public sealed class SpecialtiesController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [RequirePermission(Permission.UseApp)]
    [ProducesResponseType(typeof(IReadOnlyList<SpecialtyResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSpecialties([FromQuery] bool includeArchived, CancellationToken ct) =>
        Ok(await mediator.Send(new GetSpecialtiesQuery(includeArchived), ct));

    [HttpGet("{id:guid}")]
    [RequirePermission(Permission.UseApp)]
    [ProducesResponseType(typeof(SpecialtyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSpecialty(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new GetSpecialtyByIdQuery(id), ct));

    [HttpPost]
    [RequirePermission(Permission.ManageSetup)]
    [ProducesResponseType(typeof(SpecialtyResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateSpecialty([FromBody] CreateSpecialtyRequest request, CancellationToken ct)
    {
        var specialty = await mediator.Send(new CreateSpecialtyCommand(request.Name), ct);
        return Created($"/api/specialties/{specialty.Id}", specialty);
    }

    /// <summary>Renames a specialty; what has it shows the new name.</summary>
    [HttpPut("{id:guid}")]
    [RequirePermission(Permission.ManageSetup)]
    [ProducesResponseType(typeof(SpecialtyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateSpecialty(Guid id, [FromBody] UpdateSpecialtyRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new UpdateSpecialtyCommand(id, request.Name, request.ExpectedVersion), ct));

    /// <summary>Archives a specialty: it can't be chosen for templates or given to staff; what has it keeps it.</summary>
    [HttpPost("{id:guid}/archive")]
    [RequirePermission(Permission.ManageSetup)]
    [ProducesResponseType(typeof(SpecialtyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ArchiveSpecialty(Guid id, [FromBody] SpecialtyVersionRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new SetSpecialtyArchivedCommand(id, true, request.ExpectedVersion), ct));

    [HttpPost("{id:guid}/restore")]
    [RequirePermission(Permission.ManageSetup)]
    [ProducesResponseType(typeof(SpecialtyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RestoreSpecialty(Guid id, [FromBody] SpecialtyVersionRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new SetSpecialtyArchivedCommand(id, false, request.ExpectedVersion), ct));

    /// <summary>Deletes a specialty nothing has; one that a template, pathway or member of staff has is a 409 (archive it).</summary>
    [HttpDelete("{id:guid}")]
    [RequirePermission(Permission.ManageSetup)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteSpecialty(Guid id, CancellationToken ct)
    {
        await mediator.Send(new DeleteSpecialtyCommand(id), ct);
        return NoContent();
    }
}
