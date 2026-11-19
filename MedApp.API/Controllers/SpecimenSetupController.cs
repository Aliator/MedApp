using MedApp.API.Common.Authentication;
using MedApp.Application.Common.Identity;
using MedApp.Application.Lab.Tests;
using MedApp.Application.Specimens.Setup;
using MedApp.Contracts.Lab.Requests;
using MedApp.Contracts.Lab.Responses;
using MedApp.Contracts.Specimens.Requests;
using MedApp.Contracts.Specimens.Responses;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace MedApp.API.Controllers;

/// <summary>
/// Specimen types and lab tests (MC13; the external labs list was dropped): organisation-wide setup, which holders of ManageSetup keep and everyone reads (UseApp).
/// One a specimen has used is archived, not deleted (it can't be chosen again, and specimens that name it keep it),
/// and restored.
/// </summary>
[ApiController]
[Route("api")]
[RequirePermission(Permission.UseApp)]
public sealed class SpecimenSetupController(IMediator mediator) : ControllerBase
{
    private const string TypesTag = "Specimen types";
    private const string LabTestsTag = "Lab tests";

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
        var type = await mediator.Send(new CreateSpecimenTypeCommand(request.Name, request.ExpectedTurnaroundDays, request.SnomedCode), ct);
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
        Ok(await mediator.Send(new UpdateSpecimenTypeCommand(id, request.Name, request.ExpectedTurnaroundDays, request.SnomedCode), ct));

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

    /// <summary>Every lab test by name, with its steps; archived ones only with <c>includeArchived</c> (MC13).</summary>
    [HttpGet("lab-tests")]
    [ProducesResponseType(typeof(IReadOnlyList<LabTestResponse>), StatusCodes.Status200OK)]
    [Tags(LabTestsTag)]
    public async Task<IActionResult> GetLabTests([FromQuery] bool includeArchived, CancellationToken ct) =>
        Ok(await mediator.Send(new GetLabTestsQuery(includeArchived), ct));

    [HttpPost("lab-tests")]
    [RequirePermission(Permission.ManageSetup)]
    [ProducesResponseType(typeof(LabTestResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [Tags(LabTestsTag)]
    public async Task<IActionResult> CreateLabTest([FromBody] LabTestRequest request, CancellationToken ct)
    {
        var test = await mediator.Send(new CreateLabTestCommand(request), ct);
        return Created($"/api/lab-tests/{test.Id}", test);
    }

    /// <summary>Changes a test: its fields, and its steps by position (a step kept in its place keeps its ID).</summary>
    [HttpPut("lab-tests/{id:guid}")]
    [RequirePermission(Permission.ManageSetup)]
    [ProducesResponseType(typeof(LabTestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [Tags(LabTestsTag)]
    public async Task<IActionResult> UpdateLabTest(Guid id, [FromBody] LabTestRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new UpdateLabTestCommand(id, request), ct));

    [HttpPost("lab-tests/{id:guid}/archive")]
    [RequirePermission(Permission.ManageSetup)]
    [ProducesResponseType(typeof(LabTestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [Tags(LabTestsTag)]
    public async Task<IActionResult> ArchiveLabTest(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new SetLabTestArchivedCommand(id, true), ct));

    [HttpPost("lab-tests/{id:guid}/restore")]
    [RequirePermission(Permission.ManageSetup)]
    [ProducesResponseType(typeof(LabTestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [Tags(LabTestsTag)]
    public async Task<IActionResult> RestoreLabTest(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new SetLabTestArchivedCommand(id, false), ct));

    /// <summary>Deletes a test no lab case or sample has used; a used one is a 409 (archive it instead).</summary>
    [HttpDelete("lab-tests/{id:guid}")]
    [RequirePermission(Permission.ManageSetup)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [Tags(LabTestsTag)]
    public async Task<IActionResult> DeleteLabTest(Guid id, CancellationToken ct)
    {
        await mediator.Send(new DeleteLabTestCommand(id), ct);
        return NoContent();
    }
}
