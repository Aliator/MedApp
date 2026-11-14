using MedApp.API.Common.Authentication;
using MedApp.Application.Procedures.PatientProcedureStages.Definitions.Commands.CreatePatientProcedureStageDefinition;
using MedApp.Application.Procedures.PatientProcedureStages.Definitions.Commands.DeletePatientProcedureStageDefinition;
using MedApp.Application.Procedures.PatientProcedureStages.Definitions.Commands.SetPatientProcedureStageDefinitionArchived;
using MedApp.Application.Procedures.PatientProcedureStages.Definitions.Commands.UpdatePatientProcedureStageDefinition;
using MedApp.Application.Procedures.PatientProcedureStages.Definitions.Queries.GetAllPatientProcedureStageDefinitions;
using MedApp.Application.Procedures.PatientProcedureStages.Definitions.Queries.GetPatientProcedureStageDefinitionById;
using MedApp.Application.Procedures.PatientProcedureStages.Templates.Commands.CreatePatientProcedureStageTemplate;
using MedApp.Application.Procedures.PatientProcedureStages.Templates.Commands.DeletePatientProcedureStageTemplate;
using MedApp.Application.Procedures.PatientProcedureStages.Templates.Commands.SetPatientProcedureStageTemplateArchived;
using MedApp.Application.Procedures.PatientProcedureStages.Templates.Commands.UpdatePatientProcedureStageTemplate;
using MedApp.Application.Procedures.PatientProcedureStages.Templates.Queries.GetAllPatientProcedureStageTemplates;
using MedApp.Application.Procedures.PatientProcedureStages.Templates.Queries.GetPatientProcedureStageTemplateById;
using MedApp.Contracts.Procedures.PatientProcedureStages.Definitions.Requests;
using MedApp.Contracts.Procedures.PatientProcedureStages.Definitions.Responses;
using MedApp.Contracts.Procedures.PatientProcedureStages.Templates.Requests;
using MedApp.Contracts.Procedures.PatientProcedureStages.Templates.Responses;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedApp.API.Controllers;

/// <summary>
/// Stage definitions and procedure templates. Admins manage them; staff read them to create procedures. One that has
/// been used can't be deleted, only archived (hidden from new templates or procedures) and restored.
/// </summary>
[ApiController]
[Route("api/procedures/patient-procedure-stage")]
public sealed class PatientProcedureStagesController(IMediator mediator) : ControllerBase
{
    [HttpPost("definitions")]
    [ProducesResponseType(typeof(PatientProcedureStageDefinitionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [Authorize(Roles = "Admin")]
    [Tags("Procedure Stage Definitions")]
    public async Task<IActionResult> CreatePatientProcedureStageDefinition([FromBody] CreatePatientProcedureStageDefinitionRequest request, CancellationToken ct)
    {
        var definition = await mediator.Send(new CreatePatientProcedureStageDefinitionCommand(
            request.Name,
            request.Description,
            request.Instructions), ct);

        return Created($"/api/procedures/patient-procedure-stage/definitions/{definition.Id}", definition);
    }

    [HttpPatch("definitions/{id:guid}")]
    [ProducesResponseType(typeof(PatientProcedureStageDefinitionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [Authorize(Roles = "Admin")]
    [Tags("Procedure Stage Definitions")]
    public async Task<IActionResult> UpdatePatientProcedureStageDefinition(Guid id, [FromBody] UpdatePatientProcedureStageDefinitionRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new UpdatePatientProcedureStageDefinitionCommand(
            id,
            request.Name,
            request.Description,
            request.Instructions), ct));

    [HttpPost("definitions/{id:guid}/archive")]
    [ProducesResponseType(typeof(PatientProcedureStageDefinitionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [Authorize(Roles = "Admin")]
    [Tags("Procedure Stage Definitions")]
    public async Task<IActionResult> ArchivePatientProcedureStageDefinition(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new SetPatientProcedureStageDefinitionArchivedCommand(id, true), ct));

    [HttpPost("definitions/{id:guid}/restore")]
    [ProducesResponseType(typeof(PatientProcedureStageDefinitionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [Authorize(Roles = "Admin")]
    [Tags("Procedure Stage Definitions")]
    public async Task<IActionResult> RestorePatientProcedureStageDefinition(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new SetPatientProcedureStageDefinitionArchivedCommand(id, false), ct));

    /// <summary>Deletes a stage definition that has never been used; a used one is a 409 (archive it instead).</summary>
    [HttpDelete("definitions/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [Authorize(Roles = "Admin")]
    [Tags("Procedure Stage Definitions")]
    public async Task<IActionResult> DeletePatientProcedureStageDefinition(Guid id, CancellationToken ct)
    {
        await mediator.Send(new DeletePatientProcedureStageDefinitionCommand(id), ct);
        return NoContent();
    }

    [HttpGet("definitions")]
    [ProducesResponseType(typeof(IReadOnlyList<PatientProcedureStageDefinitionResponse>), StatusCodes.Status200OK)]
    [Authorize(Policy = AuthPolicies.Staff)]
    [Tags("Procedure Stage Definitions")]
    public async Task<IActionResult> GetAllPatientProcedureStageDefinitions([FromQuery] bool includeArchived, CancellationToken ct) =>
        Ok(await mediator.Send(new GetAllPatientProcedureStageDefinitionsQuery(includeArchived), ct));

    [HttpGet("definitions/{id:guid}")]
    [ProducesResponseType(typeof(PatientProcedureStageDefinitionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [Authorize(Policy = AuthPolicies.Staff)]
    [Tags("Procedure Stage Definitions")]
    public async Task<IActionResult> GetPatientProcedureStageDefinitionById(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new GetPatientProcedureStageDefinitionByIdQuery(id), ct));

    [HttpPost("templates")]
    [ProducesResponseType(typeof(PatientProcedureStageTemplateResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [Authorize(Roles = "Admin")]
    [Tags("Procedure Templates")]
    public async Task<IActionResult> CreatePatientProcedureStageTemplate([FromBody] CreatePatientProcedureStageTemplateRequest request, CancellationToken ct)
    {
        var template = await mediator.Send(new CreatePatientProcedureTemplateCommand(
            request.Name,
            request.StageDefinitionIdsInOrder), ct);

        return Created($"/api/procedures/patient-procedure-stage/templates/{template.Id}", template);
    }

    [HttpPatch("templates/{id:guid}")]
    [ProducesResponseType(typeof(PatientProcedureStageTemplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [Authorize(Roles = "Admin")]
    [Tags("Procedure Templates")]
    public async Task<IActionResult> UpdatePatientProcedureStageTemplate(Guid id, [FromBody] UpdatePatientProcedureStageTemplateRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new UpdatePatientProcedureTemplateCommand(
            id,
            request.Name,
            request.StageDefinitionIdsInOrder), ct));

    [HttpPost("templates/{id:guid}/archive")]
    [ProducesResponseType(typeof(PatientProcedureStageTemplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [Authorize(Roles = "Admin")]
    [Tags("Procedure Templates")]
    public async Task<IActionResult> ArchivePatientProcedureStageTemplate(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new SetPatientProcedureTemplateArchivedCommand(id, true), ct));

    [HttpPost("templates/{id:guid}/restore")]
    [ProducesResponseType(typeof(PatientProcedureStageTemplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [Authorize(Roles = "Admin")]
    [Tags("Procedure Templates")]
    public async Task<IActionResult> RestorePatientProcedureStageTemplate(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new SetPatientProcedureTemplateArchivedCommand(id, false), ct));

    /// <summary>Deletes a template no procedure was created from; a used one is a 409 (archive it instead).</summary>
    [HttpDelete("templates/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [Authorize(Roles = "Admin")]
    [Tags("Procedure Templates")]
    public async Task<IActionResult> DeletePatientProcedureStageTemplate(Guid id, CancellationToken ct)
    {
        await mediator.Send(new DeletePatientProcedureTemplateCommand(id), ct);
        return NoContent();
    }

    [HttpGet("templates")]
    [ProducesResponseType(typeof(IReadOnlyList<PatientProcedureStageTemplateResponse>), StatusCodes.Status200OK)]
    [Authorize(Policy = AuthPolicies.Staff)]
    [Tags("Procedure Templates")]
    public async Task<IActionResult> GetAllPatientProcedureStageTemplates([FromQuery] bool includeArchived, CancellationToken ct) =>
        Ok(await mediator.Send(new GetAllPatientProcedureTemplatesQuery(includeArchived), ct));

    [HttpGet("templates/{id:guid}")]
    [ProducesResponseType(typeof(PatientProcedureStageTemplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [Authorize(Policy = AuthPolicies.Staff)]
    [Tags("Procedure Templates")]
    public async Task<IActionResult> GetPatientProcedureStageTemplateById(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new GetPatientProcedureTemplateByIdQuery(id), ct));
}
