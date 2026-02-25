using MedApp.Application.Tasks.PatientTaskStages.Definitions.Commands.CreatePatientTaskStageDefinition;
using MedApp.Application.Tasks.PatientTaskStages.Definitions.Commands.DeletePatientTaskStageDefinition;
using MedApp.Application.Tasks.PatientTaskStages.Definitions.Commands.UpdatePatientTaskStageDefinition;
using MedApp.Application.Tasks.PatientTaskStages.Definitions.Queries.GetAllPatientTaskStageDefinitions;
using MedApp.Application.Tasks.PatientTaskStages.Definitions.Queries.GetPatientTaskStageDefinitionById;
using MedApp.Application.Tasks.PatientTaskStages.Templates.Commands.CreatePatientTaskStageTemplate;
using MedApp.Application.Tasks.PatientTaskStages.Templates.Commands.DeletePatientTaskStageTemplate;
using MedApp.Application.Tasks.PatientTaskStages.Templates.Commands.UpdatePatientTaskStageTemplate;
using MedApp.Application.Tasks.PatientTaskStages.Templates.Queries.GetAllPatientTaskStageTemplates;
using MedApp.Application.Tasks.PatientTaskStages.Templates.Queries.GetPatientTaskStageTemplateById;
using MedApp.Contracts.Tasks.PatientTaskStages.Definitions.Requests;
using MedApp.Contracts.Tasks.PatientTaskStages.Definitions.Responses;
using MedApp.Contracts.Tasks.PatientTaskStages.Templates.Requests;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedApp.API.Controllers;

[ApiController]
[Route("api/tasks/patient-task-stage")]
public sealed class PatientTaskStagesController(IMediator mediator) : ControllerBase
{
    [HttpPost("definitions")]
    [ProducesResponseType(typeof(PatientTaskStageDefinitionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [Authorize(Roles = "Admin")]
    [Tags("Patient Task Stage Definitions")]
    public async Task<IActionResult> CreatePatientTaskStageDefinition([FromBody] CreatePatientTaskStageDefinitionRequest request)
    {
        var command = new CreatePatientTaskStageDefinitionCommand(
            request.Name,
            request.Description,
            request.Instructions);

        var stageDefinition = await mediator.Send(command);

        return Created($"/api/tasks/patient-task-stage-definitions/{stageDefinition.Id}", stageDefinition);
    }

    [HttpPatch("definitions/{id:guid}")]
    [ProducesResponseType(typeof(PatientTaskStageDefinitionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [Authorize(Roles = "Admin")]
    [Tags("Patient Task Stage Definitions")]
    public async Task<IActionResult> UpdatePatientTaskStageDefinition(Guid id, [FromBody] UpdatePatientTaskStageDefinitionRequest request)
    {
        var command = new UpdatePatientTaskStageDefinitionCommand(
            id,
            request.Name,
            request.Description,
            request.Instructions);

        var updatedStageDefinition = await mediator.Send(command);

        return Ok(updatedStageDefinition);
    }

    [HttpDelete("definitions/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Authorize(Roles = "Admin")]
    [Tags("Patient Task Stage Definitions")]
    public async Task<IActionResult> DeletePatientTaskStageDefinition(Guid id)
    {
        var command = new DeletePatientTaskStageDefinitionCommand(id);
        var deleted = await mediator.Send(command);

        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }

    [HttpGet("definitions")]
    [ProducesResponseType(typeof(IReadOnlyList<PatientTaskStageDefinitionResponse>), StatusCodes.Status200OK)]
    [Authorize(Roles = "Admin")]
    [Tags("Patient Task Stage Definitions")]
    public async Task<IActionResult> GetAllPatientTaskStageDefinitions()
    {
        var query = new GetAllPatientTaskStageDefinitionsQuery();
        var stageDefinitions = await mediator.Send(query);

        return Ok(stageDefinitions);
    }

    [HttpGet("definitions/{id:guid}")]
    [ProducesResponseType(typeof(PatientTaskStageDefinitionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [Authorize(Roles = "Admin")]
    [Tags("Patient Task Stage Definitions")]
    public async Task<IActionResult> GetPatientTaskStageDefinitionById(Guid id)
    {
        var query = new GetPatientTaskStageDefinitionByIdQuery(id);
        var stageDefinition = await mediator.Send(query);

        return Ok(stageDefinition);
    }
    
    [HttpPost("templates")]
    [ProducesResponseType(typeof(PatientTaskStageTemplateResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [Authorize(Roles = "Admin")]
    [Tags("Patient Task Stage Templates")]
    public async Task<IActionResult> CreatePatientTaskStageTemplate([FromBody] CreatePatientTaskStageTemplateRequest request)
    {
        var command = new CreatePatientTaskTemplateCommand(
            request.Name,
            request.StageDefinitionIdsInOrder);

        var template = await mediator.Send(command);

        return Created($"/api/tasks/patient-task-stage/templates/{template.Id}", template);
    }

    [HttpPatch("templates/{id:guid}")]
    [ProducesResponseType(typeof(PatientTaskStageTemplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [Authorize(Roles = "Admin")]
    [Tags("Patient Task Stage Templates")]
    public async Task<IActionResult> UpdatePatientTaskStageTemplate(Guid id, [FromBody] UpdatePatientTaskStageTemplateRequest request)
    {
        var command = new UpdatePatientTaskTemplateCommand(
            id,
            request.Name,
            request.StageDefinitionIdsInOrder);

        var updatedTemplate = await mediator.Send(command);

        return Ok(updatedTemplate);
    }

    [HttpDelete("templates/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Authorize(Roles = "Admin")]
    [Tags("Patient Task Stage Templates")]
    public async Task<IActionResult> DeletePatientTaskStageTemplate(Guid id)
    {
        var command = new DeletePatientTaskTemplateCommand(id);
        var deleted = await mediator.Send(command);

        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }

    [HttpGet("templates")]
    [ProducesResponseType(typeof(IReadOnlyList<PatientTaskStageTemplateResponse>), StatusCodes.Status200OK)]
    [Authorize(Roles = "Admin")]
    [Tags("Patient Task Stage Templates")]
    public async Task<IActionResult> GetAllPatientTaskStageTemplates()
    {
        var query = new GetAllPatientTaskTemplatesQuery();
        var templates = await mediator.Send(query);

        return Ok(templates);
    }

    [HttpGet("templates/{id:guid}")]
    [ProducesResponseType(typeof(PatientTaskStageTemplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [Authorize(Roles = "Admin")]
    [Tags("Patient Task Stage Templates")]
    public async Task<IActionResult> GetPatientTaskStageTemplateById(Guid id)
    {
        var query = new GetPatientTaskTemplateByIdQuery(id);
        var template = await mediator.Send(query);

        return Ok(template);
    }
    
}