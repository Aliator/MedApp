using MedApp.API.Common.Authentication;
using MedApp.Application.Pathways.PatientPathwayStages.Definitions.Commands.CreatePatientPathwayStageDefinition;
using MedApp.Application.Pathways.PatientPathwayStages.Definitions.Commands.DeletePatientPathwayStageDefinition;
using MedApp.Application.Pathways.PatientPathwayStages.Definitions.Commands.SetPatientPathwayStageDefinitionArchived;
using MedApp.Application.Pathways.PatientPathwayStages.Definitions.Commands.UpdatePatientPathwayStageDefinition;
using MedApp.Application.Pathways.PatientPathwayStages.Definitions.Queries.GetAllPatientPathwayStageDefinitions;
using MedApp.Application.Pathways.PatientPathwayStages.Definitions.Queries.GetPatientPathwayStageDefinitionById;
using MedApp.Application.Pathways.PatientPathwayStages.Templates.Commands.CreatePathwayTemplate;
using MedApp.Application.Pathways.PatientPathwayStages.Templates.Commands.DeletePathwayTemplate;
using MedApp.Application.Pathways.PatientPathwayStages.Templates.Commands.SetPathwayTemplateArchived;
using MedApp.Application.Pathways.PatientPathwayStages.Templates.Commands.UpdatePathwayTemplate;
using MedApp.Application.Pathways.PatientPathwayStages.Templates.Queries.GetAllPathwayTemplates;
using MedApp.Application.Pathways.PatientPathwayStages.Templates.Queries.GetPathwayTemplateById;
using MedApp.Contracts.Pathways.PatientPathwayStages.Definitions.Requests;
using MedApp.Contracts.Pathways.PatientPathwayStages.Definitions.Responses;
using MedApp.Contracts.Pathways.PatientPathwayStages.Templates.Requests;
using MedApp.Contracts.Pathways.PatientPathwayStages.Templates.Responses;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedApp.API.Controllers;

/// <summary>
/// Stage definitions and pathway templates. Admins manage them; staff read them to create pathways. One that has
/// been used can't be deleted, only archived (hidden from new templates or pathways) and restored.
/// </summary>
[ApiController]
[Route("api/pathways/patient-pathway-stage")]
public sealed class PatientPathwayStagesController(IMediator mediator) : ControllerBase
{
    [HttpPost("definitions")]
    [ProducesResponseType(typeof(PatientPathwayStageDefinitionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [Authorize(Roles = "Admin")]
    [Tags("Pathway Stage Definitions")]
    public async Task<IActionResult> CreatePatientPathwayStageDefinition([FromBody] CreatePatientPathwayStageDefinitionRequest request, CancellationToken ct)
    {
        var definition = await mediator.Send(new CreatePatientPathwayStageDefinitionCommand(
            request.Name,
            request.Description,
            request.Instructions,
            request.SnomedCode,
            request.SnomedTerm,
            request.VisitRequirement), ct);

        return Created($"/api/pathways/patient-pathway-stage/definitions/{definition.Id}", definition);
    }

    [HttpPatch("definitions/{id:guid}")]
    [ProducesResponseType(typeof(PatientPathwayStageDefinitionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [Authorize(Roles = "Admin")]
    [Tags("Pathway Stage Definitions")]
    public async Task<IActionResult> UpdatePatientPathwayStageDefinition(Guid id, [FromBody] UpdatePatientPathwayStageDefinitionRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new UpdatePatientPathwayStageDefinitionCommand(
            id,
            request.Name,
            request.Description,
            request.Instructions,
            request.SnomedCode,
            request.SnomedTerm,
            request.VisitRequirement), ct));

    [HttpPost("definitions/{id:guid}/archive")]
    [ProducesResponseType(typeof(PatientPathwayStageDefinitionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [Authorize(Roles = "Admin")]
    [Tags("Pathway Stage Definitions")]
    public async Task<IActionResult> ArchivePatientPathwayStageDefinition(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new SetPatientPathwayStageDefinitionArchivedCommand(id, true), ct));

    [HttpPost("definitions/{id:guid}/restore")]
    [ProducesResponseType(typeof(PatientPathwayStageDefinitionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [Authorize(Roles = "Admin")]
    [Tags("Pathway Stage Definitions")]
    public async Task<IActionResult> RestorePatientPathwayStageDefinition(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new SetPatientPathwayStageDefinitionArchivedCommand(id, false), ct));

    /// <summary>Deletes a stage definition that has never been used; a used one is a 409 (archive it instead).</summary>
    [HttpDelete("definitions/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [Authorize(Roles = "Admin")]
    [Tags("Pathway Stage Definitions")]
    public async Task<IActionResult> DeletePatientPathwayStageDefinition(Guid id, CancellationToken ct)
    {
        await mediator.Send(new DeletePatientPathwayStageDefinitionCommand(id), ct);
        return NoContent();
    }

    [HttpGet("definitions")]
    [ProducesResponseType(typeof(IReadOnlyList<PatientPathwayStageDefinitionResponse>), StatusCodes.Status200OK)]
    [Authorize(Policy = AuthPolicies.Staff)]
    [Tags("Pathway Stage Definitions")]
    public async Task<IActionResult> GetAllPatientPathwayStageDefinitions([FromQuery] bool includeArchived, CancellationToken ct) =>
        Ok(await mediator.Send(new GetAllPatientPathwayStageDefinitionsQuery(includeArchived), ct));

    [HttpGet("definitions/{id:guid}")]
    [ProducesResponseType(typeof(PatientPathwayStageDefinitionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [Authorize(Policy = AuthPolicies.Staff)]
    [Tags("Pathway Stage Definitions")]
    public async Task<IActionResult> GetPatientPathwayStageDefinitionById(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new GetPatientPathwayStageDefinitionByIdQuery(id), ct));

    [HttpPost("templates")]
    [ProducesResponseType(typeof(PathwayTemplateResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [Authorize(Roles = "Admin")]
    [Tags("Pathway Templates")]
    public async Task<IActionResult> CreatePathwayTemplate([FromBody] CreatePathwayTemplateRequest request, CancellationToken ct)
    {
        var template = await mediator.Send(new CreatePathwayTemplateCommand(
            request.Name,
            request.SpecialtyId,
            request.StageDefinitionIdsInOrder), ct);

        return Created($"/api/pathways/patient-pathway-stage/templates/{template.Id}", template);
    }

    [HttpPatch("templates/{id:guid}")]
    [ProducesResponseType(typeof(PathwayTemplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [Authorize(Roles = "Admin")]
    [Tags("Pathway Templates")]
    public async Task<IActionResult> UpdatePathwayTemplate(Guid id, [FromBody] UpdatePathwayTemplateRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new UpdatePathwayTemplateCommand(
            id,
            request.Name,
            request.SpecialtyId,
            request.StageDefinitionIdsInOrder), ct));

    [HttpPost("templates/{id:guid}/archive")]
    [ProducesResponseType(typeof(PathwayTemplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [Authorize(Roles = "Admin")]
    [Tags("Pathway Templates")]
    public async Task<IActionResult> ArchivePathwayTemplate(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new SetPathwayTemplateArchivedCommand(id, true), ct));

    [HttpPost("templates/{id:guid}/restore")]
    [ProducesResponseType(typeof(PathwayTemplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [Authorize(Roles = "Admin")]
    [Tags("Pathway Templates")]
    public async Task<IActionResult> RestorePathwayTemplate(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new SetPathwayTemplateArchivedCommand(id, false), ct));

    /// <summary>Deletes a template no pathway was created from; a used one is a 409 (archive it instead).</summary>
    [HttpDelete("templates/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [Authorize(Roles = "Admin")]
    [Tags("Pathway Templates")]
    public async Task<IActionResult> DeletePathwayTemplate(Guid id, CancellationToken ct)
    {
        await mediator.Send(new DeletePathwayTemplateCommand(id), ct);
        return NoContent();
    }

    [HttpGet("templates")]
    [ProducesResponseType(typeof(IReadOnlyList<PathwayTemplateResponse>), StatusCodes.Status200OK)]
    [Authorize(Policy = AuthPolicies.Staff)]
    [Tags("Pathway Templates")]
    public async Task<IActionResult> GetAllPathwayTemplates([FromQuery] bool includeArchived, CancellationToken ct) =>
        Ok(await mediator.Send(new GetAllPathwayTemplatesQuery(includeArchived), ct));

    [HttpGet("templates/{id:guid}")]
    [ProducesResponseType(typeof(PathwayTemplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [Authorize(Policy = AuthPolicies.Staff)]
    [Tags("Pathway Templates")]
    public async Task<IActionResult> GetPathwayTemplateById(Guid id, CancellationToken ct) =>
        Ok(await mediator.Send(new GetPathwayTemplateByIdQuery(id), ct));
}
