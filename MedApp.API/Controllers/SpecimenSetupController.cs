using MedApp.API.Common.Authentication;
using MedApp.Application.Common.Identity;
using MedApp.Application.Specimens.Setup;
using MedApp.Contracts.Specimens.Requests;
using MedApp.Contracts.Specimens.Responses;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace MedApp.API.Controllers;

/// <summary>
/// Specimen types and labs: organisation-wide setup, which holders of ManageSetup keep and everyone reads (UseApp).
/// One a specimen has used is archived, not deleted (it can't be chosen again, and specimens that name it keep it),
/// and restored.
/// </summary>
[ApiController]
[Route("api")]
[RequirePermission(Permission.UseApp)]
public sealed class SpecimenSetupController(IMediator mediator) : ControllerBase
{
    private const string TypesTag = "Specimen types";
    private const string LabsTag = "Laboratories";

    /// <summary>Every specimen type by name; archived ones only with <c>includeArchived</c>.</summary>
    [HttpGet("specimen-types")]
    [ProducesResponseType(typeof(IReadOnlyList<SpecimenTypeResponse>), StatusCodes.Status200OK)]
    [Tags(TypesTag)]
    public async Task<IActionResult> GetTypes([FromQuery] bool includeArchived, CancellationToken ct) =>
        Ok(await mediator.Send(new GetSpecimenTypesQuery(includeArchived), ct));

    [HttpPost("specimen-types")]
    [RequirePermission(Permission.ManageSetup)]
    [ProducesResponseType(typeof(SpecimenTypeResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [Tags(TypesTag)]
    public async Task<IActionResult> CreateType([FromBody] SpecimenTypeRequest request, CancellationToken ct)
    {
        var type = await mediator.Send(new CreateSpecimenTypeCommand(request.Name, request.ExpectedTurnaroundDays), ct);
        return Created($"/api/specimen-types/{type.Id}", type);
    }

    [HttpPut("specimen-types/{id:guid}")]
    [RequirePermission(Permission.ManageSetup)]
    [ProducesResponseType(typeof(SpecimenTypeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [Tags(TypesTag)]
    public async Task<IActionResult> UpdateType(Guid id, [FromBody] SpecimenTypeRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new UpdateSpecimenTypeCommand(id, request.Name, request.ExpectedTurnaroundDays), ct));

    [HttpPost("specimen-types/{id:guid}/archive")]
    [RequirePermission(Permission.ManageSetup)]
    [ProducesResponseType(typeof(SpecimenTypeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [Tags(TypesTag)]
    public async Task<IActionResult> ArchiveType(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new SetSpecimenTypeArchivedCommand(id, true), ct));

    [HttpPost("specimen-types/{id:guid}/restore")]
    [RequirePermission(Permission.ManageSetup)]
    [ProducesResponseType(typeof(SpecimenTypeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [Tags(TypesTag)]
    public async Task<IActionResult> RestoreType(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new SetSpecimenTypeArchivedCommand(id, false), ct));

    /// <summary>Deletes a type no specimen has; a used one is a 409 (archive it instead).</summary>
    [HttpDelete("specimen-types/{id:guid}")]
    [RequirePermission(Permission.ManageSetup)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [Tags(TypesTag)]
    public async Task<IActionResult> DeleteType(Guid id, CancellationToken ct)
    {
        await mediator.Send(new DeleteSpecimenTypeCommand(id), ct);
        return NoContent();
    }

    /// <summary>Every lab by name; archived ones only with <c>includeArchived</c>.</summary>
    [HttpGet("laboratories")]
    [ProducesResponseType(typeof(IReadOnlyList<LaboratoryResponse>), StatusCodes.Status200OK)]
    [Tags(LabsTag)]
    public async Task<IActionResult> GetLaboratories([FromQuery] bool includeArchived, CancellationToken ct) =>
        Ok(await mediator.Send(new GetLaboratoriesQuery(includeArchived), ct));

    [HttpPost("laboratories")]
    [RequirePermission(Permission.ManageSetup)]
    [ProducesResponseType(typeof(LaboratoryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [Tags(LabsTag)]
    public async Task<IActionResult> CreateLaboratory([FromBody] LaboratoryRequest request, CancellationToken ct)
    {
        var laboratory = await mediator.Send(new CreateLaboratoryCommand(request.Name, request.UsesMedApp), ct);
        return Created($"/api/laboratories/{laboratory.Id}", laboratory);
    }

    [HttpPut("laboratories/{id:guid}")]
    [RequirePermission(Permission.ManageSetup)]
    [ProducesResponseType(typeof(LaboratoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [Tags(LabsTag)]
    public async Task<IActionResult> UpdateLaboratory(Guid id, [FromBody] LaboratoryRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new UpdateLaboratoryCommand(id, request.Name, request.UsesMedApp), ct));

    [HttpPost("laboratories/{id:guid}/archive")]
    [RequirePermission(Permission.ManageSetup)]
    [ProducesResponseType(typeof(LaboratoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [Tags(LabsTag)]
    public async Task<IActionResult> ArchiveLaboratory(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new SetLaboratoryArchivedCommand(id, true), ct));

    [HttpPost("laboratories/{id:guid}/restore")]
    [RequirePermission(Permission.ManageSetup)]
    [ProducesResponseType(typeof(LaboratoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [Tags(LabsTag)]
    public async Task<IActionResult> RestoreLaboratory(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new SetLaboratoryArchivedCommand(id, false), ct));

    /// <summary>Deletes a lab no specimen was sent to; a used one is a 409 (archive it instead).</summary>
    [HttpDelete("laboratories/{id:guid}")]
    [RequirePermission(Permission.ManageSetup)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [Tags(LabsTag)]
    public async Task<IActionResult> DeleteLaboratory(Guid id, CancellationToken ct)
    {
        await mediator.Send(new DeleteLaboratoryCommand(id), ct);
        return NoContent();
    }
}
